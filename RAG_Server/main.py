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
import threading
import time

# ==============================
# 環境設定（請透過 .env 或環境變數提供）
# ==============================
QDRANT_HOST = os.getenv("QDRANT_HOST", "localhost")
QDRANT_PORT = int(os.getenv("QDRANT_PORT", "6333"))
# 測試可設定 QDRANT_LOCATION=":memory:"；設定後優先於 host / port
QDRANT_LOCATION = os.getenv("QDRANT_LOCATION", "")
# 藍綠切換：新模型使用新 collection，舊 collection（rag_knowledge，384 維）保留供回退
COLLECTION_NAME = os.getenv("QDRANT_COLLECTION", "rag_knowledge_bge_m3")
# 中英雙語與跨語言檢索（2026-09-25 以雙語測試集比較後選定）
EMBEDDING_MODEL = os.getenv("EMBEDDING_MODEL", "BAAI/bge-m3")
CORS_ORIGINS = [o.strip() for o in os.getenv("CORS_ORIGINS", "*").split(",") if o.strip()]
# .NET API 與本服務共用的內部金鑰；未設定時所有受保護端點一律拒絕（fail-closed）
INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")
DISABLE_MODEL_PRELOAD = os.getenv("RAG_DISABLE_MODEL_PRELOAD", "") == "1"

SPACE_ID_KEY = "space_id"

# ==============================
# 初始化 FastAPI
# ==============================
app = FastAPI(title="RAG API")

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

# 全域變數用於快取模型
_model = None
_qa_model = None
_model_loading = False
_qa_model_loading = False
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
# 模型載入
# ==============================
def load_models_background():
    """在背景執行緒中載入模型"""
    global _model, _qa_model, _model_loading, _qa_model_loading

    if not _model_loading:
        _model_loading = True
        try:
            print("背景載入 SentenceTransformer 模型...")
            from sentence_transformers import SentenceTransformer
            _model = SentenceTransformer(EMBEDDING_MODEL)
            print("SentenceTransformer 模型載入完成")
        except Exception as e:
            print(f"SentenceTransformer 載入失敗: {e}")
        _model_loading = False

    if not _qa_model_loading:
        _qa_model_loading = True
        try:
            print("背景載入 QA 模型...")
            from transformers import pipeline
            _qa_model = pipeline("question-answering", model="uer/roberta-base-chinese-extractive-qa")
            print("QA 模型載入完成")
        except Exception as e:
            print(f"QA 模型載入失敗: {e}")
        _qa_model_loading = False


def get_sentence_model():
    """取得 sentence transformer 模型"""
    global _model, _model_loading
    if _model is None and not _model_loading:
        thread = threading.Thread(target=load_models_background)
        thread.daemon = True
        thread.start()
    return _model


def get_qa_model():
    """取得 QA 模型"""
    global _qa_model, _qa_model_loading
    if _qa_model is None and not _qa_model_loading:
        thread = threading.Thread(target=load_models_background)
        thread.daemon = True
        thread.start()
    return _qa_model


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
def embedding_dimension(model) -> int:
    return model.get_sentence_embedding_dimension()


def encode(model, text: str) -> List[float]:
    return model.encode(text, normalize_embeddings=True).tolist()


def ensure_collection(vector_size: Optional[int] = None, collection_name: str = COLLECTION_NAME) -> bool:
    """
    確保 Collection 與 space_id payload index 存在。
    vector_size 為 None（讀取 / 刪除）且 collection 不存在時回傳 False，不建立；
    collection 維度與目前模型不符時拒絕操作，避免混入不相容的向量。
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
            raise HTTPException(status_code=500, detail=f"Collection 維度 {existing} 與 embedding 模型維度 {vector_size} 不符")
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
    model = get_sentence_model()

    if model is None:
        raise HTTPException(status_code=503, detail="模型正在載入中，請稍後再試")
    ensure_collection(embedding_dimension(model))

    # 既有 ID 必須屬於同一個 Space，避免以猜測的 ID 覆寫其他 Space 的向量
    reference_ids = [q.referenceId for q in request.questions if q.referenceId]
    if reference_ids:
        existing = client.retrieve(collection_name=COLLECTION_NAME, ids=reference_ids, with_payload=True)
        if any((p.payload or {}).get(SPACE_ID_KEY) != request.space_id for p in existing):
            raise HTTPException(status_code=403, detail="向量不屬於此範圍")

    vectors = []
    for q in request.questions:
        try:
            vector = encode(model, q.question)
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
    model = get_sentence_model()

    if model is None:
        raise HTTPException(status_code=503, detail="模型正在載入中，請稍後再試")
    if not ensure_collection(embedding_dimension(model)):
        raise HTTPException(status_code=400, detail="找不到相關內容")

    query_vector = encode(model, request.question)

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

    # 選擇最相似的一筆
    best_match = max(results, key=lambda r: r.score)

    if best_match and best_match.score > 0.7:
        print("執行直接匹配")
        answer = best_match.payload["answer"]
        confidence = best_match.score
        method = "direct_match"
    else:
        print("執行內容摘要")
        answer = f"根據相關內容：{context[:500]}..." if len(context) > 500 else context
        confidence = best_match.score
        method = "context_summary"

    # 去除句子重複
    answer = dedup_text(answer)

    print(f"回答方式: {method}, 信心度: {confidence}")
    return {
        "answer": answer,
        "confidence": confidence,
        "related": [r.payload["question"] for r in results],
        "method": method
    }


def search_knowledge(request: SearchRequest):
    """只檢索、不生成：回傳指定 Space 內的命中結果與相似度分數"""
    model = get_sentence_model()

    if model is None:
        raise HTTPException(status_code=503, detail="模型正在載入中，請稍後再試")
    if not ensure_collection(embedding_dimension(model)):
        return {"hits": []}

    extra = []
    if request.type:
        extra.append(models.FieldCondition(key="type", match=models.MatchValue(value=request.type)))

    results = client.search(
        collection_name=COLLECTION_NAME,
        query_vector=encode(model, request.query),
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
        "sentence_model_status": "loaded" if _model else "loading" if _model_loading else "not_loaded",
        "qa_model_status": "loaded" if _qa_model else "loading" if _qa_model_loading else "not_loaded"
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
    return {
        "sentence_model": "loaded" if _model else "loading" if _model_loading else "not_loaded",
        "qa_model": "loaded" if _qa_model else "loading" if _qa_model_loading else "not_loaded"
    }


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


# ==============================
# 啟動事件：背景載入模型
# ==============================
@app.on_event("startup")
async def startup_event():
    if DISABLE_MODEL_PRELOAD:
        return

    def delayed_load():
        time.sleep(2)
        load_models_background()
    thread = threading.Thread(target=delayed_load)
    thread.daemon = True
    thread.start()
