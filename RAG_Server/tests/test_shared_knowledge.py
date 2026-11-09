"""
公開知識：自己的 Space 答不出來才查 shared_space_id；私人內容永遠不會因此跨 Space。
"""
from types import SimpleNamespace

import pytest
from fastapi import HTTPException

import main


def hit(score, answer):
    return SimpleNamespace(score=score, payload={"answer": answer, "question": "q"})


@pytest.fixture
def spaces(monkeypatch):
    data = {}
    searched = []

    def search(**kwargs):
        space = kwargs["query_filter"].must[0].match.value
        searched.append(space)
        return data.get(space, [])

    monkeypatch.setattr(main.client, "search", search)
    monkeypatch.setattr(main, "ensure_collection", lambda *a, **k: True)
    monkeypatch.setattr(main, "embed_text", lambda text: [0.0])
    monkeypatch.setattr(main, "generate_with_llm", lambda *a, **k: None)
    return data, searched


def ask(**kwargs):
    return main.generate_answer(main.AskRequest(question="怎麼用", **kwargs))


def test_own_answer_never_touches_shared(spaces):
    data, searched = spaces
    data[1] = [hit(0.9, "我的答案")]
    data[99] = [hit(0.99, "公開答案")]
    assert ask(space_id=1, shared_space_id=99)["answer"] == "我的答案"
    assert searched == [1]


def test_falls_back_to_shared_with_notice(spaces):
    data, _ = spaces
    data[99] = [hit(0.9, "公開答案")]
    answer = ask(space_id=1, shared_space_id=99)["answer"]
    assert answer.startswith(main.SHARED_ANSWER_PREFIX)
    assert answer.endswith("公開答案")


def test_low_score_shared_is_not_used(spaces):
    data, _ = spaces
    data[99] = [hit(0.1, "公開答案")]
    with pytest.raises(HTTPException):
        ask(space_id=1, shared_space_id=99)


def test_without_shared_only_own_space(spaces):
    data, searched = spaces
    data[2] = [hit(0.99, "別人的私人答案")]
    with pytest.raises(HTTPException):
        ask(space_id=1)
    assert searched == [1]
