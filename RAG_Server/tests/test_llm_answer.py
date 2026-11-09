"""
選用的 LLM 生成回答：有設定 OLLAMA_CHAT_MODEL 且沒有 direct match 時才呼叫；
LLM 失敗或回應空白時一律退回 context_summary，/ask 不會因為 LLM 而失敗。
"""
import hashlib

import numpy as np
import pytest
import requests
from fastapi.testclient import TestClient

import main

KEY = {"X-Internal-Api-Key": "test-internal-key"}


def fake_embed(text):
    seed = int(hashlib.sha256(text.encode("utf-8")).hexdigest()[:8], 16)
    return np.random.default_rng(seed).random(1024).tolist()


class FakeResponse:
    def __init__(self, body):
        self.body = body

    def raise_for_status(self):
        pass

    def json(self):
        return self.body


@pytest.fixture(autouse=True)
def fresh_collection(monkeypatch):
    monkeypatch.setattr(main, "embed_text", fake_embed)
    monkeypatch.setattr(main, "OLLAMA_CHAT_MODEL", "test-chat-model")
    if main.COLLECTION_NAME in [c.name for c in main.client.get_collections().collections]:
        main.client.delete_collection(main.COLLECTION_NAME)
    main._space_index_ready = False
    yield


@pytest.fixture
def api():
    with TestClient(main.app) as c:
        yield c


@pytest.fixture
def chat(monkeypatch):
    """取代對 Ollama /api/chat 的呼叫，記下送出的內容；reply 可以是字串或要丟出的例外"""
    calls = []
    state = {"reply": "整理後的回答"}

    def fake_post(url, json=None, timeout=None):
        calls.append({"url": url, "json": json})
        if isinstance(state["reply"], Exception):
            raise state["reply"]
        return FakeResponse({"message": {"role": "assistant", "content": state["reply"]}})

    monkeypatch.setattr(main.requests, "post", fake_post)
    return calls, state


def write(api, question, answer, qid):
    item = {"id": qid, "qaTypeId": 2, "qaItemTypeId": 3, "question": question, "answer": answer}
    response = api.post("/questionsToVector", json={"space_id": 1, "questions": [item]}, headers=KEY)
    assert response.status_code == 200


def ask_without_direct_match(api, monkeypatch, question):
    monkeypatch.setattr(main, "DIRECT_MATCH_THRESHOLD", 2.0)  # 不可能達到 → 不會 direct match
    monkeypatch.setattr(main, "SIMILAR_SCORE_THRESHOLD", 0.0)
    return api.post("/ask", json={"space_id": 1, "question": question}, headers=KEY).json()


def test_llm_answers_from_the_retrieved_answers(api, monkeypatch, chat):
    calls, _ = chat
    write(api, "問題一", "## 答案一\n\n來源：筆記 entry#1", 1)
    write(api, "問題二", "## 答案二", 2)

    body = ask_without_direct_match(api, monkeypatch, "使用者的問題")

    assert body["method"] == "llm_generate"
    assert body["answer"] == "整理後的回答"
    sent = calls[0]["json"]
    assert calls[0]["url"].endswith("/api/chat")
    assert sent["model"] == "test-chat-model" and sent["stream"] is False
    prompt = sent["messages"][-1]["content"]
    assert "## 答案一" in prompt and "## 答案二" in prompt and "來源：筆記 entry#1" in prompt
    assert prompt.endswith("問題：使用者的問題")


def test_think_block_is_removed_from_the_answer(api, monkeypatch, chat):
    _, state = chat
    state["reply"] = "<think>\n先想一想\n</think>\n\n真正的回答"
    write(api, "問題一", "答案一", 1)

    assert ask_without_direct_match(api, monkeypatch, "問題")["answer"] == "真正的回答"


@pytest.mark.parametrize("reply", [requests.Timeout("逾時"), "", "<think>只有思考</think>"])
def test_falls_back_to_summary_when_the_llm_fails_or_says_nothing(api, monkeypatch, chat, reply):
    _, state = chat
    state["reply"] = reply
    write(api, "問題一", "答案一", 1)

    body = ask_without_direct_match(api, monkeypatch, "問題")

    assert body["method"] == "context_summary"
    assert "答案一" in body["answer"]


def test_direct_match_does_not_call_the_llm(api, chat):
    calls, _ = chat
    write(api, "專案目錄結構怎麼放？", "存好的答案", 1)

    body = api.post("/ask", json={"space_id": 1, "question": "專案目錄結構怎麼放？"}, headers=KEY).json()

    assert body["method"] == "direct_match" and body["answer"] == "存好的答案"
    assert calls == []


def test_without_a_chat_model_the_llm_is_never_called(api, monkeypatch, chat):
    calls, _ = chat
    monkeypatch.setattr(main, "OLLAMA_CHAT_MODEL", "")
    write(api, "問題一", "答案一", 1)

    assert ask_without_direct_match(api, monkeypatch, "問題")["method"] == "context_summary"
    assert calls == []


def test_long_references_are_cut_before_sending(api, monkeypatch, chat):
    calls, _ = chat
    monkeypatch.setattr(main, "GENERATE_CONTEXT_MAX_CHARS", 300)
    write(api, "問題一", "\n\n".join("很長的段落" * 20 for _ in range(10)), 1)

    ask_without_direct_match(api, monkeypatch, "問題")

    assert len(calls[0]["json"]["messages"][-1]["content"]) < 400
