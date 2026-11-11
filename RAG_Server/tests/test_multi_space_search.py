"""
檢索範圍：.NET 依 membership 算出 space_ids 之後，rag-ai 只在這些 Space 內搜尋；沒帶就維持只找 space_id。
"""
from types import SimpleNamespace

import pytest
from pydantic import ValidationError

import main


def hit(score, space_id, answer="a"):
    return SimpleNamespace(
        id="p", score=score, payload={"answer": answer, "question": f"q{space_id}", "space_id": space_id, "referenId": "1", "type": "t"}
    )


@pytest.fixture
def captured(monkeypatch):
    calls = []

    def search(**kwargs):
        calls.append(kwargs["query_filter"])
        return [hit(0.9, 1)]

    monkeypatch.setattr(main.client, "search", search)
    monkeypatch.setattr(main, "ensure_collection", lambda *a, **k: True)
    monkeypatch.setattr(main, "embed_text", lambda text: [0.0])
    monkeypatch.setattr(main, "generate_with_llm", lambda *a, **k: None)
    return calls


def space_condition(flt):
    return next(c for c in flt.must if getattr(c, "key", None) == main.SPACE_ID_KEY).match


def test_effective_spaces_always_include_the_primary_one():
    assert main.search_space_ids(1, None) == [1]
    assert main.search_space_ids(1, []) == [1]
    assert main.search_space_ids(3, [2, 1]) == [1, 2, 3]
    assert main.search_space_ids(1, [1, 1, 2]) == [1, 2]


def test_without_space_ids_the_query_is_unchanged(captured):
    main.search_knowledge(main.SearchRequest(space_id=1, query="x"))

    match = space_condition(captured[0])
    assert isinstance(match, main.models.MatchValue)
    assert match.value == 1


def test_search_covers_exactly_the_given_spaces(captured):
    main.search_knowledge(main.SearchRequest(space_id=1, space_ids=[1, 2, 4], query="x"))

    match = space_condition(captured[0])
    assert isinstance(match, main.models.MatchAny)
    assert match.any == [1, 2, 4]


def test_ask_covers_the_given_spaces(captured):
    main.generate_answer(main.AskRequest(space_id=2, space_ids=[5, 2], question="怎麼用"))

    match = space_condition(captured[0])
    assert isinstance(match, main.models.MatchAny)
    assert match.any == [2, 5]


def test_type_filter_is_kept_alongside_the_space_filter(captured):
    main.search_knowledge(main.SearchRequest(space_id=1, space_ids=[1, 2], query="x", type="faq"))

    keys = [getattr(c, "key", None) for c in captured[0].must]
    assert main.SPACE_ID_KEY in keys and "type" in keys


@pytest.mark.parametrize("bad", [[0], [1, -1], list(range(1, main.MAX_SEARCH_SPACES + 2))])
@pytest.mark.parametrize("model", [main.SearchRequest, main.AskRequest])
def test_invalid_space_ids_are_rejected(model, bad):
    # 非正數：條件沒有意義；過多：避免被拿來當成放大器
    kwargs = {"query": "x"} if model is main.SearchRequest else {"question": "x"}
    with pytest.raises(ValidationError):
        model(space_id=1, space_ids=bad, **kwargs)
