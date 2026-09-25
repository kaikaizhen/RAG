import hashlib

import numpy as np
import pytest
from fastapi.testclient import TestClient
from qdrant_client.http import models

import main

KEY = {"X-Internal-Api-Key": "test-internal-key"}
SPACE_A = 1
SPACE_B = 2


class FakeModel:
    """以文字雜湊產生固定 384 維向量；相同文字得到相同向量（cosine = 1）"""

    def get_sentence_embedding_dimension(self):
        return 384

    def encode(self, text, **kwargs):
        seed = int(hashlib.sha256(text.encode("utf-8")).hexdigest()[:8], 16)
        return np.random.default_rng(seed).random(384)


@pytest.fixture(autouse=True)
def fresh_collection(monkeypatch):
    monkeypatch.setattr(main, "get_sentence_model", lambda: FakeModel())
    if main.COLLECTION_NAME in [c.name for c in main.client.get_collections().collections]:
        main.client.delete_collection(main.COLLECTION_NAME)
    main._space_index_ready = False
    yield


@pytest.fixture
def api():
    with TestClient(main.app) as c:
        yield c


def write(api, space_id, question="Q-A", answer="A-A", reference_id=None, qid=1):
    item = {"id": qid, "qaTypeId": 2, "qaItemTypeId": 3, "question": question, "answer": answer}
    if reference_id:
        item["referenceId"] = reference_id
    return api.post("/questionsToVector", json={"space_id": space_id, "questions": [item]}, headers=KEY)


def payload_of(point_id):
    points = main.client.retrieve(main.COLLECTION_NAME, ids=[point_id], with_payload=True)
    return points[0].payload if points else None


# ---------- 內部驗證 ----------

PROTECTED = [
    ("post", "/questionsToVector", {"space_id": 1, "questions": []}),
    ("post", "/ask", {"space_id": 1, "question": "q"}),
    ("delete", "/questions/00000000-0000-0000-0000-000000000001?space_id=1", None),
    ("get", "/questions?space_id=1", None),
    ("get", "/collections", None),
    ("get", "/model_status", None),
    ("post", "/search", {"space_id": 1, "query": "q"}),
]


@pytest.mark.parametrize("method,url,body", PROTECTED)
@pytest.mark.parametrize("headers", [{}, {"X-Internal-Api-Key": "wrong"}, {"X-Internal-Api-Key": ""}])
def test_protected_endpoints_reject_missing_or_wrong_key(api, method, url, body, headers):
    response = api.request(method.upper(), url, json=body, headers=headers)
    assert response.status_code == 401


def test_unset_server_key_rejects_every_call(api, monkeypatch):
    monkeypatch.setattr(main, "INTERNAL_API_KEY", "")
    assert api.get("/collections", headers=KEY).status_code == 401
    assert api.get("/collections", headers={"X-Internal-Api-Key": ""}).status_code == 401


def test_health_endpoint_is_open(api):
    assert api.get("/").status_code == 200


def test_whole_type_delete_endpoint_is_removed(api):
    assert api.delete("/delete_type/2", headers=KEY).status_code in (404, 405)


# ---------- space_id 為必要且由呼叫端（.NET）指定 ----------

@pytest.mark.parametrize("space_id", [None, 0, -1])
def test_write_requires_positive_space_id(api, space_id):
    body = {"questions": [{"id": 1, "qaTypeId": 2, "qaItemTypeId": 3, "question": "q", "answer": "a"}]}
    if space_id is not None:
        body["space_id"] = space_id
    assert api.post("/questionsToVector", json=body, headers=KEY).status_code == 422


@pytest.mark.parametrize("url", ["/questions", "/questions?space_id=0"])
def test_list_requires_positive_space_id(api, url):
    assert api.get(url, headers=KEY).status_code == 422


def test_delete_requires_space_id(api):
    assert api.delete("/questions/00000000-0000-0000-0000-000000000001", headers=KEY).status_code == 422


def test_write_stores_space_id_payload(api):
    response = write(api, SPACE_A)

    assert response.status_code == 200
    vector_id = response.json()["vectorIds"][0]
    assert payload_of(vector_id)[main.SPACE_ID_KEY] == SPACE_A


# ---------- Space A / Space B 隔離 ----------

def test_space_b_cannot_search_space_a(api):
    write(api, SPACE_A, question="唯一的問題")

    own = api.post("/ask", json={"space_id": SPACE_A, "question": "唯一的問題"}, headers=KEY)
    other = api.post("/ask", json={"space_id": SPACE_B, "question": "唯一的問題"}, headers=KEY)

    assert own.status_code == 200
    assert own.json()["answer"].startswith("A-A")
    assert other.status_code == 400


def test_space_b_cannot_list_space_a(api):
    write(api, SPACE_A)

    assert api.get(f"/questions?space_id={SPACE_A}", headers=KEY).json()["count"] == 1
    assert api.get(f"/questions?space_id={SPACE_B}", headers=KEY).json()["count"] == 0


def test_space_b_cannot_delete_space_a_point_by_guessed_id(api):
    vector_id = write(api, SPACE_A).json()["vectorIds"][0]

    response = api.delete(f"/questions/{vector_id}?space_id={SPACE_B}", headers=KEY)

    assert response.status_code == 404
    assert payload_of(vector_id) is not None


def test_space_a_can_delete_own_point(api):
    vector_id = write(api, SPACE_A).json()["vectorIds"][0]

    assert api.delete(f"/questions/{vector_id}?space_id={SPACE_A}", headers=KEY).status_code == 200
    assert payload_of(vector_id) is None


def test_space_b_cannot_overwrite_space_a_point_by_guessed_id(api):
    vector_id = write(api, SPACE_A).json()["vectorIds"][0]

    response = write(api, SPACE_B, question="hacked", answer="hacked", reference_id=vector_id)

    assert response.status_code == 403
    payload = payload_of(vector_id)
    assert payload[main.SPACE_ID_KEY] == SPACE_A
    assert payload["question"] == "Q-A"


def test_space_a_can_update_own_point_in_place(api):
    vector_id = write(api, SPACE_A).json()["vectorIds"][0]

    response = write(api, SPACE_A, question="Q-A2", answer="A-A2", reference_id=vector_id)

    assert response.status_code == 200
    assert response.json()["vectorIds"] == [vector_id]
    assert payload_of(vector_id)["question"] == "Q-A2"


def test_unscoped_legacy_point_is_invisible_and_cannot_be_claimed(api):
    main.ensure_collection(384)
    legacy_id = "11111111-1111-1111-1111-111111111111"
    main.client.upsert(main.COLLECTION_NAME, points=[models.PointStruct(
        id=legacy_id, vector=FakeModel().encode("legacy").tolist(),
        payload={"question": "legacy", "answer": "legacy", "type": "2"})])

    assert api.get(f"/questions?space_id={SPACE_A}", headers=KEY).json()["count"] == 0
    assert api.post("/ask", json={"space_id": SPACE_A, "question": "legacy"}, headers=KEY).status_code == 400
    assert api.delete(f"/questions/{legacy_id}?space_id={SPACE_A}", headers=KEY).status_code == 404
    assert write(api, SPACE_A, reference_id=legacy_id).status_code == 403
    assert main.SPACE_ID_KEY not in payload_of(legacy_id)


def test_type_filter_is_combined_with_space_filter(api):
    write(api, SPACE_A, question="同樣的問題", answer="A 的答案")

    response = api.post("/ask", json={"space_id": SPACE_B, "question": "同樣的問題", "type": "2"}, headers=KEY)

    assert response.status_code == 400


# ---------- /search：只檢索、不生成 ----------

def test_search_returns_scored_hits_of_own_space_only_without_synthesis(api):
    write(api, SPACE_A, question="請假流程", answer="送出假單", qid=11)
    write(api, SPACE_B, question="請假流程", answer="B 的答案", qid=22)

    response = api.post("/search", json={"space_id": SPACE_A, "query": "請假流程", "limit": 5}, headers=KEY)

    assert response.status_code == 200
    hits = response.json()["hits"]
    assert [h["reference_id"] for h in hits] == ["11"]
    assert hits[0]["answer"] == "送出假單"
    assert hits[0]["score"] > 0.99
    assert set(response.json()) == {"hits"}


def test_search_in_empty_space_returns_no_hits(api):
    write(api, SPACE_A)

    assert api.post("/search", json={"space_id": SPACE_B, "query": "Q-A"}, headers=KEY).json() == {"hits": []}


@pytest.mark.parametrize("body", [
    {"query": "q"},
    {"space_id": 0, "query": "q"},
    {"space_id": 1, "query": ""},
    {"space_id": 1, "query": "q", "limit": 0},
    {"space_id": 1, "query": "q", "limit": 51},
])
def test_search_validates_scope_and_limits(api, body):
    assert api.post("/search", json=body, headers=KEY).status_code == 422


# ---------- embedding 模型 / collection 維度 ----------

def test_write_is_refused_when_collection_dimension_differs_from_model(api):
    main.client.create_collection(main.COLLECTION_NAME, vectors_config=models.VectorParams(size=8, distance=models.Distance.COSINE))

    response = write(api, SPACE_A)

    assert response.status_code == 500
    assert "維度" in response.json()["detail"]
    assert main.client.count(main.COLLECTION_NAME).count == 0


def test_reads_do_not_create_missing_collection(api):
    assert api.get(f"/questions?space_id={SPACE_A}", headers=KEY).json()["count"] == 0
    assert api.delete(f"/questions/00000000-0000-0000-0000-000000000001?space_id={SPACE_A}", headers=KEY).status_code == 404
    assert not main.client.collection_exists(main.COLLECTION_NAME)


def test_default_embedding_model_is_bilingual_bge_m3():
    assert main.EMBEDDING_MODEL == "BAAI/bge-m3"
    assert main.COLLECTION_NAME == "rag_knowledge_bge_m3"
