import hashlib
import json
import sys
from pathlib import Path

import numpy as np
import pytest
from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

import main  # noqa: E402
import reindex_from_export as tool  # noqa: E402

KEY = {"X-Internal-Api-Key": "test-internal-key"}


class FakeModel:
    def get_sentence_embedding_dimension(self):
        return 384

    def encode(self, text, **kwargs):
        seed = int(hashlib.sha256(text.encode("utf-8")).hexdigest()[:8], 16)
        return np.random.default_rng(seed).random(384)


@pytest.fixture
def api(monkeypatch):
    monkeypatch.setattr(main, "get_sentence_model", lambda: FakeModel())
    if main.client.collection_exists(main.COLLECTION_NAME):
        main.client.delete_collection(main.COLLECTION_NAME)
    main._space_index_ready = False
    with TestClient(main.app) as c:
        post = lambda path, body: (lambda r: (r.raise_for_status(), r.json())[1])(c.post(path, json=body, headers=KEY))
        get = lambda path: (lambda r: (r.raise_for_status(), r.json())[1])(c.get(path, headers=KEY))
        yield post, get


def export(tmp_path, rows):
    path = tmp_path / "export.jsonl"
    path.write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows), encoding="utf-8")
    return str(path)


def row(i, space, vector=None):
    return {"id": i, "space_id": space, "vector_id": vector or f"aaaaaaaa-0000-0000-0000-{i:012d}", "qa_type_id": 2,
            "qa_item_type_id": 3, "question": f"問題 {i}", "answer": f"答案 {i}"}


def test_dry_run_writes_nothing(tmp_path, api):
    post, get = api
    assert tool.main([export(tmp_path, [row(1, 1)])], post=post, get=get) == 0
    assert not main.client.collection_exists(main.COLLECTION_NAME)


def test_apply_rebuilds_points_per_space_with_original_ids_and_reconciles(tmp_path, api, capsys):
    post, get = api
    rows = [row(1, 1), row(2, 1), row(3, 2)]

    assert tool.main([export(tmp_path, rows), "--apply", "--batch-size", "1"], post=post, get=get) == 0

    assert "reconciliation=OK" in capsys.readouterr().out
    points = main.client.retrieve(main.COLLECTION_NAME, ids=[r["vector_id"] for r in rows], with_payload=True)
    assert sorted((str(p.id), p.payload["space_id"], p.payload["referenId"]) for p in points) == sorted(
        (r["vector_id"], r["space_id"], str(r["id"])) for r in rows)


def test_apply_is_idempotent(tmp_path, api):
    post, get = api
    path = export(tmp_path, [row(1, 1), row(2, 2)])

    assert tool.main([path, "--apply"], post=post, get=get) == 0
    assert tool.main([path, "--apply"], post=post, get=get) == 0
    assert main.client.count(main.COLLECTION_NAME).count == 2


def test_reconciliation_reports_extra_points(tmp_path, api, capsys):
    post, get = api
    tool.main([export(tmp_path, [row(1, 1), row(2, 1)]), "--apply"], post=post, get=get)

    assert tool.main([export(tmp_path, [row(1, 1)]), "--apply"], post=post, get=get) == 3
    assert "reconciliation=MISMATCH" in capsys.readouterr().out


@pytest.mark.parametrize("bad", [
    {"id": 1, "space_id": 1},
    {**row(1, 1), "space_id": 0},
    {**row(1, 1), "vector_id": ""},
])
def test_invalid_export_rows_are_rejected(tmp_path, bad):
    with pytest.raises(ValueError):
        tool.read_export(export(tmp_path, [bad]))


def test_duplicate_vector_ids_are_rejected(tmp_path):
    with pytest.raises(ValueError):
        tool.read_export(export(tmp_path, [row(1, 1, "same"), row(2, 1, "same")]))


def test_missing_key_refuses_to_run(tmp_path, monkeypatch):
    monkeypatch.delenv("INTERNAL_API_KEY", raising=False)
    assert tool.main([export(tmp_path, [row(1, 1)]), "--apply"]) == 2
