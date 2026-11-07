from fastapi import Depends, FastAPI, Header, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field, field_validator
from datetime import datetime
from qdrant_client import QdrantClient
from qdrant_client.http import models
from typing import Optional, List
import hmac
import os
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
def dedup_text(text: str) -> str:
    """移除重複句子"""
    sentences = [s.strip() for s in text.split("。") if s.strip()]
    seen = set()
    result = []
    for s in sentences:
        if s not in seen:
            seen.add(s)
            result.append(s)
    return "。".join(result) + ("。" if result else "")


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


def generate_answer(request: AskRequest):
    """查詢 Qdrant 並產生回答（僅搜尋指定 Space）"""
    if not ensure_collection(EMBEDDING_DIMENSION):
        raise HTTPException(status_code=400, detail="找不到相關內容")

    query_vector = embed_text(request.question)

    extra = []
    if request.type:
        extra.append(models.FieldCondition(key="type", match=models.MatchValue(value=request.type)))

    results = client.search(
        collection_name=COLLECTION_NAME,
        query_vector=query_vector,
        limit=3,
        with_payload=True,
        query_filter=space_filter(request.space_id, *extra)
    )

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

    context = "\n".join(unique_answers)

    # results 依分數由高到低；direct match 需同時超過門檻且與第二名拉開差距
    best_match = results[0]
    confidence = best_match.score
    margin = confidence - results[1].score if len(results) > 1 else confidence

    if confidence < SIMILAR_SCORE_THRESHOLD:
        raise HTTPException(status_code=400, detail="找不到足夠可靠的相關內容")

    if confidence >= DIRECT_MATCH_THRESHOLD and margin >= DIRECT_MATCH_MARGIN:
        print("執行直接匹配")
        answer = best_match.payload["answer"]
        method = "direct_match"
    else:
        print("執行內容摘要")
        answer = f"根據相關內容：{context[:500]}..." if len(context) > 500 else context
        method = "context_summary"

    # 去除句子重複
    answer = dedup_text(answer)

    print(f"回答方式: {method}, 信心度: {confidence}, 與第二名差距: {margin}")
    return {
        "answer": answer,
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

