from fastapi import Depends, FastAPI, Header, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field, field_validator
from datetime import datetime
from qdrant_client import QdrantClient
from qdrant_client.http import models
from typing import Optional, List
import hmac
import os
import re
import uuid
import requests

# ==============================
# 環境設定（請透過 .env 或環境變數提供）
# ==============================
QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")
QDRANT_PORT = int(os.getenv("QDRANT_PORT", "6333"))
# 測試可設定 QDRANT_LOCATION=":memory:"；設定後優先於 host / port
QDRANT_LOCATION = os.getenv("QDRANT_LOCATION", "")
# 更換 embedding 模型必須使用新 collection（舊 rag_knowledge_bge_m3 為 bge-m3 向量，保留供回退）
COLLECTION_NAME = os.getenv("QDRANT_COLLECTION", "rag_knowledge_v2")
# Embedding 由遠端 Ollama 提供，本服務不載入任何本地模型；Ollama 失敗時直接回 503，不 fallback
OLLAMA_BASE_URL = os.getenv("OLLAMA_BASE_URL", "http://192.168.0.188:11434").rstrip("/")
OLLAMA_EMBED_MODEL = os.getenv("OLLAMA_EMBED_MODEL", "qwen3-embedding:0.6b")
EMBEDDING_DIMENSION = int(os.getenv("EMBEDDING_DIMENSION", "1024"))
OLLAMA_TIMEOUT_SECONDS = float(os.getenv("OLLAMA_TIMEOUT_SECONDS", "30"))
OLLAMA_HEALTH_TIMEOUT_SECONDS = float(os.getenv("OLLAMA_HEALTH_TIMEOUT_SECONDS", "3"))
# /ask 判斷門檻（與 .NET ContextRuntime 由 compose/.env 注入同一組值）；依 qwen3-embedding:0.6b 校準（見 docs/calibration）
DIRECT_MATCH_THRESHOLD = float(os.getenv("DIRECT_MATCH_THRESHOLD", "0.7"))
DIRECT_MATCH_MARGIN = float(os.getenv("DIRECT_MATCH_MARGIN", "0.05"))
SIMILAR_SCORE_THRESHOLD = float(os.getenv("SIMILAR_SCORE_THRESHOLD", "0.50"))
# 摘要模式（context_summary）回覆的最大字數；只在區塊邊界截斷，所以實際可能略少
SUMMARY_MAX_CHARS = int(os.getenv("SUMMARY_MAX_CHARS", "2000"))
# 生成回答（選用）：設定 OLLAMA_CHAT_MODEL 後，沒有 direct match 時改由 LLM 依檢索到的內容回答；
# 未設定或 LLM 失敗時維持 context_summary，不影響原本的行為
OLLAMA_CHAT_MODEL = os.getenv("OLLAMA_CHAT_MODEL", "")
OLLAMA_CHAT_TIMEOUT_SECONDS = float(os.getenv("OLLAMA_CHAT_TIMEOUT_SECONDS", "60"))
# 送給 LLM 的參考資料上限（字數），避免超過模型的 context
GENERATE_CONTEXT_MAX_CHARS = int(os.getenv("GENERATE_CONTEXT_MAX_CHARS", "6000"))
CORS_ORIGINS = [o.strip() for o in os.getenv("CORS_ORIGINS", "*").split(",") if o.strip()]
# .NET API 與本服務共用的內部金鑰；未設定時所有受保護端點一律拒絕（fail-closed）
INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")

SPACE_ID_KEY = "space_id"

# ==============================
# 初始化 FastAPI
# ==============================
# root_path 讓服務可以掛在 Nginx 的 /ai/ 前綴之下，docs / openapi.json 連結才會正確
app = FastAPI(title="RAG API", root_path=os.getenv("RAG_ROOT_PATH", ""))

# 啟用 CORS（跨域設定）
app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ==============================
# 初始化 Qdrant
# ==============================
client = QdrantClient(location=QDRANT_LOCATION) if QDRANT_LOCATION else QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)

_space_index_ready = False


# ==============================
# 內部服務驗證
# ==============================
def require_internal_key(x_internal_api_key: Optional[str] = Header(default=None)):
    """僅允許持有內部金鑰的 .NET API 呼叫；以常數時間比對"""
    if not INTERNAL_API_KEY or not x_internal_api_key:
        raise HTTPException(status_code=401, detail="Unauthorized")
    if not hmac.compare_digest(x_internal_api_key.encode("utf-8"), INTERNAL_API_KEY.encode("utf-8")):
        raise HTTPException(status_code=401, detail="Unauthorized")


# ==============================
# Embedding（遠端 Ollama）
# ==============================
def embed_text(text: str) -> List[float]:
    """呼叫 Ollama /api/embed；逾時、錯誤、格式或維度不符一律回 503，不 fallback 本地模型"""
    try:
        response = requests.post(
            f"{OLLAMA_BASE_URL}/api/embed",
            json={"model": OLLAMA_EMBED_MODEL, "input": text},
            timeout=OLLAMA_TIMEOUT_SECONDS,
        )
        response.raise_for_status()
        embeddings = response.json().get("embeddings")
    except (requests.RequestException, ValueError) as e:
        print(f"Ollama embedding 失敗: {e}")
        raise HTTPException(status_code=503, detail="Embedding 服務無法使用，請稍後再試")
    if not embeddings or not isinstance(embeddings[0], list):
        raise HTTPException(status_code=503, detail="Embedding 服務回應格式錯誤")
    vector = embeddings[0]
    if len(vector) != EMBEDDING_DIMENSION:
        print(f"Embedding 維度 {len(vector)} 與設定 {EMBEDDING_DIMENSION} 不符")
        raise HTTPException(status_code=503, detail=f"Embedding 維度 {len(vector)} 與設定 {EMBEDDING_DIMENSION} 不符")
    return vector


GENERATE_SYSTEM_PROMPT = """你是知識庫助理，只能根據使用者訊息裡的「參考資料」回答問題。
- 使用繁體中文，以 Markdown 回答，直接回應問題，不要逐篇複述參考資料。
- 參考資料沒有提到的內容不要補充或猜測；資料不足以回答時，說明缺少哪些資訊。
- 參考資料標示為推測或未確認的內容，回答時也要說明。
- 參考資料結尾的「來源：」行，回答有用到哪一篇，就在回答最後原樣列出那一行。"""

THINK_BLOCK = re.compile(r"<think>.*?</think>", re.DOTALL)


def generate_with_llm(question: str, answers: List[str]) -> Optional[str]:
    """把檢索到的答案交給 Ollama 的聊天模型整理成回答；未啟用、失敗或回應空白時回 None，由呼叫端退回摘要模式"""
    if not OLLAMA_CHAT_MODEL:
        return None
    references = "\n\n".join(f"### 參考資料 {i}\n\n{answer}" for i, answer in enumerate(answers, 1))
    if len(references) > GENERATE_CONTEXT_MAX_CHARS:
        references = truncate_markdown(references, GENERATE_CONTEXT_MAX_CHARS)
    try:
        response = requests.post(
            f"{OLLAMA_BASE_URL}/api/chat",
            json={
                "model": OLLAMA_CHAT_MODEL,
                "stream": False,
                "messages": [
                    {"role": "system", "content": GENERATE_SYSTEM_PROMPT},
                    {"role": "user", "content": f"參考資料：\n\n{references}\n\n問題：{question}"},
                ],
            },
            timeout=OLLAMA_CHAT_TIMEOUT_SECONDS,
        )
        response.raise_for_status()
        content = (response.json().get("message") or {}).get("content") or ""
    except (requests.RequestException, ValueError, AttributeError) as e:
        print(f"Ollama 生成回答失敗，改用內容摘要: {e}")
        return None
    # 會思考的模型（例如 qwen3）可能把思考過程放在 <think> 裡，不屬於回答
    content = normalize_newlines(THINK_BLOCK.sub("", content)).strip()
    return content or None


def ollama_health() -> dict:
    """檢查 Ollama 是否可連線且已安裝指定的 embedding 模型"""
    try:
        response = requests.get(f"{OLLAMA_BASE_URL}/api/tags", timeout=OLLAMA_HEALTH_TIMEOUT_SECONDS)
        response.raise_for_status()
        names = {m.get("name") for m in response.json().get("models", [])}
    except (requests.RequestException, ValueError):
        return {"status": "unreachable", "model": OLLAMA_EMBED_MODEL, "dimension": EMBEDDING_DIMENSION}
    status = "ok" if OLLAMA_EMBED_MODEL in names or f"{OLLAMA_EMBED_MODEL}:latest" in names else "model_missing"
    return {"status": status, "model": OLLAMA_EMBED_MODEL, "dimension": EMBEDDING_DIMENSION}


# ==============================
# 資料模型
# ==============================
class CaseInsensitiveModel(BaseModel):
    class Config:
        alias_generator = lambda s: s.lower()
        allow_population_by_field_name = True
        populate_by_name = True


class QdrantPayload(CaseInsensitiveModel):
    id: Optional[str] = Field(default=None)
    referenId: Optional[str] = Field(default=None)
    space_id: int
    type: str
    question: str
    answer: str
    source: Optional[str] = None
    version: Optional[str] = "v1.0"
    language: Optional[str] = "zh-TW"
    created_at: datetime = Field(default_factory=datetime.utcnow)

    @field_validator("id", "referenId", mode="before")
    def ensure_uuid(cls, v):
        if not v:
            return str(uuid.uuid4())
        return v


class QuestionRequest(CaseInsensitiveModel):
    id: int
    referenceId: Optional[str] = None
    qaTypeId: int
    qaItemTypeId: int
    question: str
    answer: str


class QuestionListRequest(CaseInsensitiveModel):
    # 由 .NET API 依已驗證的 SpaceContext 決定；本服務只信任持有內部金鑰的呼叫者
    space_id: int = Field(gt=0)
    questions: List[QuestionRequest]


class AskRequest(CaseInsensitiveModel):
    space_id: int = Field(gt=0)
    # 公開知識的 Space：自己的 Space 答不出來才查。由 .NET 依伺服器設定帶入，使用者無法指定
    shared_space_id: Optional[int] = Field(default=None, gt=0)
    question: str
    type: Optional[str] = None


class SearchRequest(CaseInsensitiveModel):
    space_id: int = Field(gt=0)
    query: str = Field(min_length=1, max_length=2000)
    limit: int = Field(default=10, ge=1, le=50)
    type: Optional[str] = None


# ==============================
# 工具函式
# ==============================
# ==============================
# 回答格式（答案是 Markdown，前端會渲染；這裡的處理不可以破壞換行、縮排與程式碼區塊）
# ==============================
FENCE_MARKERS = ("```", "~~~")


def normalize_newlines(text: str) -> str:
    return (text or "").replace("\r\n", "\n").replace("\r", "\n")


def split_blocks(text: str) -> List[str]:
    """
    以空行把文字切成區塊；``` / ~~~ 圍起來的程式碼區塊視為一個整體（裡面的空行不切），
    區塊內的換行與縮排一律保留。
    """
    blocks: List[str] = []
    current: List[str] = []
    fence: Optional[str] = None
    for line in normalize_newlines(text).split("\n"):
        stripped = line.strip()
        marker = next((m for m in FENCE_MARKERS if stripped.startswith(m)), None)
        if fence is None and marker:
            fence = marker
        elif fence is not None and stripped.startswith(fence):
            fence = None
        if fence is None and not stripped:
            if current:
                blocks.append("\n".join(current))
                current = []
            continue
        current.append(line)
    if current:
        blocks.append("\n".join(current))
    return blocks


def dedup_blocks(text: str) -> str:
    """去除完全相同的段落（以區塊為單位，不改動區塊內容）；區塊之間以一個空行分隔"""
    seen = set()
    unique = []
    for block in split_blocks(text):
        key = block.strip()
        if key and key not in seen:
            seen.add(key)
            unique.append(block)
    return "\n\n".join(unique)


def truncate_markdown(text: str, limit: int) -> str:
    """
    把 Markdown 截短到約 limit 字：只在區塊（段落 / 完整的程式碼區塊）邊界截斷；
    若連第一個區塊都超過 limit，就在行的邊界截斷，並補上結尾的程式碼圍欄，避免後面的內容全被當成程式碼。
    被截短時結尾加上「…」。
    """
    text = normalize_newlines(text)
    if len(text) <= limit:
        return text
    kept: List[str] = []
    total = 0
    for block in split_blocks(text):
        extra = len(block) + (2 if kept else 0)
        if total + extra > limit:
            break
        kept.append(block)
        total += extra
    if kept:
        return "\n\n".join(kept) + "\n\n…"

    # 第一個區塊就太長：在行的邊界截斷
    lines: List[str] = []
    used = 0
    for line in split_blocks(text)[0].split("\n"):
        if used + len(line) + 1 > limit and lines:
            break
        lines.append(line[:limit])
        used += len(line) + 1
    fence_open = sum(1 for line in lines if line.strip().startswith(FENCE_MARKERS)) % 2 == 1
    return "\n".join(lines) + ("\n```" if fence_open else "") + "\n\n…"


def space_filter(space_id: int, *conditions) -> models.Filter:
    """所有讀寫刪都必須帶的 Space 範圍條件"""
    return models.Filter(
        must=[models.FieldCondition(key=SPACE_ID_KEY, match=models.MatchValue(value=space_id)), *conditions]
    )


def find_points_in_space(space_id: int, point_ids: List[str]):
    """只在指定 Space 內依 ID 取得 point（其他 Space 的同 ID point 視為不存在）"""
    points, _ = client.scroll(
        collection_name=COLLECTION_NAME,
        scroll_filter=space_filter(space_id, models.HasIdCondition(has_id=point_ids)),
        limit=len(point_ids),
        with_payload=True,
        with_vectors=False,
    )
    return points


# ==============================
# 核心功能
# ==============================
def ensure_collection(vector_size: Optional[int] = None, collection_name: str = COLLECTION_NAME) -> bool:
    """
    確保 Collection 與 space_id payload index 存在。
    vector_size 為 None（讀取 / 刪除）且 collection 不存在時回傳 False，不建立；
    collection 維度與 embedding 設定不符時拒絕操作，避免混入不相容的向量。
    """
    global _space_index_ready
    if not client.collection_exists(collection_name):
        if vector_size is None:
            return False
        client.create_collection(
            collection_name=collection_name,
            vectors_config=models.VectorParams(size=vector_size, distance=models.Distance.COSINE)
        )
        _space_index_ready = False
    elif vector_size is not None:
        existing = client.get_collection(collection_name).config.params.vectors.size
        if existing != vector_size:
            raise HTTPException(status_code=500, detail=f"Collection 維度 {existing} 與 embedding 設定維度 {vector_size} 不符")
    if not _space_index_ready:
        client.create_payload_index(
            collection_name=collection_name,
            field_name=SPACE_ID_KEY,
            field_schema=models.PayloadSchemaType.INTEGER,
        )
        _space_index_ready = True
    return True


def insert_vectors(collection_name: str, vectors: List[dict]):
    """批次寫入 Qdrant"""
    client.upsert(
        collection_name=collection_name,
        points=models.Batch(
            ids=[v["id"] for v in vectors],
            vectors=[v["vector"] for v in vectors],
            payloads=[v["payload"] for v in vectors]
        )
    )


def process_questions_to_vector(request: QuestionListRequest):
    """將問題轉向量並寫入 Qdrant（僅限指定 Space）"""
    ensure_collection(EMBEDDING_DIMENSION)

    # 既有 ID 必須屬於同一個 Space，避免以猜測的 ID 覆寫其他 Space 的向量
    reference_ids = [q.referenceId for q in request.questions if q.referenceId]
    if reference_ids:
        existing = client.retrieve(collection_name=COLLECTION_NAME, ids=reference_ids, with_payload=True)
        if any((p.payload or {}).get(SPACE_ID_KEY) != request.space_id for p in existing):
            raise HTTPException(status_code=403, detail="向量不屬於此範圍")

    vectors = []
    for q in request.questions:
        try:
            vector = embed_text(q.question)
            payload = QdrantPayload(
                id=q.referenceId if q.referenceId else str(uuid.uuid4()),
                referenId=str(q.id),
                space_id=request.space_id,
                type=str(q.qaTypeId),
                question=q.question,
                answer=q.answer,
            )
            vectors.append({
                "id": payload.id,
                "vector": vector,
                "payload": payload.model_dump(mode="json")
            })
        except HTTPException:
            raise
        except Exception as e:
            print(f"處理問題時發生錯誤: {e}")
            raise HTTPException(status_code=400, detail=f"處理問題時發生錯誤")

    if vectors:
        insert_vectors(COLLECTION_NAME, vectors)
        return {"status": "success", "vectorIds": [str(v["payload"]["id"]) for v in vectors]}
    else:
        raise HTTPException(status_code=400, detail="沒有成功處理任何問題")


SHARED_ANSWER_PREFIX = "> 這段回答來自**公開知識**，不是你自己的筆記。\n\n"


def generate_answer(request: AskRequest):
    """查詢 Qdrant 並產生回答（僅搜尋指定 Space）"""
    if not ensure_collection(EMBEDDING_DIMENSION):
        raise HTTPException(status_code=400, detail="找不到相關內容")

    query_vector = embed_text(request.question)

    extra = []
    if request.type:
        extra.append(models.FieldCondition(key="type", match=models.MatchValue(value=request.type)))

    def search_space(space_id: int):
        return client.search(
            collection_name=COLLECTION_NAME,
            query_vector=query_vector,
            limit=3,
            with_payload=True,
            query_filter=space_filter(space_id, *extra)
        )

    results = search_space(request.space_id)

    # 自己的筆記優先；答不出來才看公開知識，分數一樣要過門檻
    from_shared = False
    if request.shared_space_id and (not results or results[0].score < SIMILAR_SCORE_THRESHOLD):
        shared = search_space(request.shared_space_id)
        if shared and shared[0].score >= SIMILAR_SCORE_THRESHOLD:
            results = shared
            from_shared = True

    if not results:
        raise HTTPException(status_code=400, detail="找不到相關內容")

    # 去除重複的答案內容
    unique_answers = []
    seen_answers = set()
    for r in results:
        ans = r.payload.get("answer", "")
        if ans not in seen_answers:
            seen_answers.add(ans)
            unique_answers.append(ans)

    # 摘要模式要用的內容：答案之間以空行分隔（Markdown 的段落 / 清單 / 程式碼區塊才不會黏在一起）
    context = dedup_blocks("\n\n".join(unique_answers))

    # results 依分數由高到低；direct match 需同時超過門檻且與第二名拉開差距
    best_match = results[0]
    confidence = best_match.score
    margin = confidence - results[1].score if len(results) > 1 else confidence

    if confidence < SIMILAR_SCORE_THRESHOLD:
        raise HTTPException(status_code=400, detail="找不到足夠可靠的相關內容")

    if confidence >= DIRECT_MATCH_THRESHOLD and margin >= DIRECT_MATCH_MARGIN:
        print("執行直接匹配")
        # 直接回覆儲存的答案，原樣不動（換行、縮排、程式碼區塊都要保留）
        answer = normalize_newlines(best_match.payload["answer"])
        method = "direct_match"
    else:
        generated = generate_with_llm(request.question, unique_answers)
        if generated:
            print("執行 LLM 生成回答")
            answer = generated
            method = "llm_generate"
        else:
            print("執行內容摘要")
            summary = truncate_markdown(context, SUMMARY_MAX_CHARS)
            answer = f"根據相關內容：\n\n{summary}" if summary != context else context
            method = "context_summary"

    print(f"回答方式: {method}, 信心度: {confidence}, 與第二名差距: {margin}")
    return {
        "answer": f"{SHARED_ANSWER_PREFIX}{answer}" if from_shared else answer,
        "confidence": confidence,
        "related": [r.payload["question"] for r in results],
        "method": method
    }


def search_knowledge(request: SearchRequest):
    """只檢索、不生成：回傳指定 Space 內的命中結果與相似度分數"""
    if not ensure_collection(EMBEDDING_DIMENSION):
        return {"hits": []}

    extra = []
    if request.type:
        extra.append(models.FieldCondition(key="type", match=models.MatchValue(value=request.type)))

    results = client.search(
        collection_name=COLLECTION_NAME,
        query_vector=embed_text(request.query),
        limit=request.limit,
        with_payload=True,
        query_filter=space_filter(request.space_id, *extra)
    )

    return {
        "hits": [
            {
                "id": str(r.id),
                "reference_id": (r.payload or {}).get("referenId"),
                "type": (r.payload or {}).get("type"),
                "question": (r.payload or {}).get("question", ""),
                "answer": (r.payload or {}).get("answer", ""),
                "score": r.score,
            }
            for r in results
        ]
    }


def delete_question_by_id(question_id: str, space_id: int):
    """刪除指定 Space 內的問題向量；其他 Space 的同 ID point 不受影響"""
    if not ensure_collection() or not find_points_in_space(space_id, [question_id]):
        raise HTTPException(status_code=404, detail=f"找不到 ID: {question_id}")
    client.delete(
        collection_name=COLLECTION_NAME,
        points_selector=models.FilterSelector(
            filter=space_filter(space_id, models.HasIdCondition(has_id=[question_id]))
        )
    )
    return {"status": "success", "message": f"已刪除問題 ID: {question_id}"}


def get_collections():
    """列出所有 Qdrant Collections"""
    cols = [c.name for c in client.get_collections().collections]
    return {"collections": cols}


# ==============================
# API 層
# ==============================
@app.get("/")
def read_root():
    return {
        "message": "RAG API is running",
        "embedding": ollama_health()
    }


@app.post("/questionsToVector", dependencies=[Depends(require_internal_key)])
def api_questions_to_vector(request: QuestionListRequest):
    return process_questions_to_vector(request)


@app.post("/ask", dependencies=[Depends(require_internal_key)])
def api_ask(request: AskRequest):
    return generate_answer(request)


@app.post("/search", dependencies=[Depends(require_internal_key)])
def api_search(request: SearchRequest):
    return search_knowledge(request)


@app.delete("/questions/{question_id}", dependencies=[Depends(require_internal_key)])
def api_delete_question(question_id: str, space_id: int = Query(gt=0)):
    return delete_question_by_id(question_id, space_id)


@app.get("/collections", dependencies=[Depends(require_internal_key)])
def api_get_collections():
    return get_collections()


@app.get("/model_status", dependencies=[Depends(require_internal_key)])
def get_model_status():
    return {"embedding": {"provider": "ollama", **ollama_health()}}


@app.get("/questions", dependencies=[Depends(require_internal_key)])
def list_questions(space_id: int = Query(gt=0), limit: int = Query(default=100, gt=0, le=1000)):
    """列出指定 Space 的問題"""
    if not ensure_collection():
        return {"status": "success", "count": 0, "questions": []}
    result = client.scroll(
        collection_name=COLLECTION_NAME,
        scroll_filter=space_filter(space_id),
        limit=limit,
        with_payload=True,
        with_vectors=False
    )
    questions = []
    for point in result[0]:
        if point.payload:
            questions.append({
                "id": point.id,
                "question": point.payload.get("question", ""),
                "answer": point.payload.get("answer", ""),
                "type": point.payload.get("type", ""),
                "created_at": point.payload.get("created_at", "")
            })
    return {"status": "success", "count": len(questions), "questions": questions}

