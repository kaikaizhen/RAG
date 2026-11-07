import sys
from pathlib import Path

import pytest
from qdrant_client.http import models

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

import backfill_space_id as tool  # noqa: E402
import main  # noqa: E402

A = "11111111-1111-1111-1111-111111111111"
B = "22222222-2222-2222-2222-222222222222"
ORPHAN = "33333333-3333-3333-3333-333333333333"
MISSING = "44444444-4444-4444-4444-444444444444"


@pytest.fixture(autouse=True)
def legacy_points(monkeypatch):
    monkeypatch.setattr(tool, "connect", lambda: main.client)
    monkeypatch.setenv("QDRANT_COLLECTION", main.COLLECTION_NAME)
    if main.COLLECTION_NAME in [c.name for c in main.client.get_collections().collections]:
        main.client.delete_collection(main.COLLECTION_NAME)
    main.client.create_collection(main.COLLECTION_NAME, vectors_config=models.VectorParams(size=4, distance=models.Distance.COSINE))
    main.client.upsert(main.COLLECTION_NAME, points=[
        models.PointStruct(id=pid, vector=[0.1, 0.2, 0.3, 0.4], payload={"question": pid})
        for pid in (A, B, ORPHAN)
    ])


def mapping_file(tmp_path, rows):
    path = tmp_path / "mapping.tsv"
    path.write_text("".join(f"{v}\t{s}\n" for v, s in rows), encoding="utf-8")
    return str(path)


def space_of(pid):
    return (main.client.retrieve(main.COLLECTION_NAME, ids=[pid], with_payload=True)[0].payload or {}).get("space_id")


def test_dry_run_reports_and_changes_nothing(tmp_path, capsys):
    assert tool.main([mapping_file(tmp_path, [(A, 1), (B, 2), (MISSING, 1)])]) == 0

    out = capsys.readouterr().out
    assert "missing_in_qdrant=1" in out
    assert "orphans_in_qdrant=1" in out
    assert space_of(A) is None and space_of(B) is None


def test_apply_sets_space_ids_and_leaves_orphans_unscoped(tmp_path):
    assert tool.main([mapping_file(tmp_path, [(A, 1), (B, 2)]), "--apply"]) == 0

    assert (space_of(A), space_of(B), space_of(ORPHAN)) == (1, 2, None)


def test_apply_is_idempotent(tmp_path):
    path = mapping_file(tmp_path, [(A, 1)])
    assert tool.main([path, "--apply"]) == 0
    assert tool.main([path, "--apply"]) == 0
    assert space_of(A) == 1


def test_apply_aborts_on_conflicting_existing_space(tmp_path):
    main.client.set_payload(main.COLLECTION_NAME, payload={"space_id": 9}, points=[A])

    assert tool.main([mapping_file(tmp_path, [(A, 1), (B, 2)]), "--apply"]) == 2
    assert (space_of(A), space_of(B)) == (9, None)


def test_rollback_removes_space_ids(tmp_path):
    path = mapping_file(tmp_path, [(A, 1), (B, 2)])
    tool.main([path, "--apply"])

    assert tool.main([path, "--rollback"]) == 0
    assert (space_of(A), space_of(B)) == (None, None)


@pytest.mark.parametrize("line", ["only-one-column\n", f"{A}\t0\n", f"{A}\tx\n"])
def test_malformed_mapping_is_rejected(tmp_path, line):
    path = tmp_path / "bad.tsv"
    path.write_text(line, encoding="utf-8")
    with pytest.raises(ValueError):
        tool.read_mapping(str(path))
