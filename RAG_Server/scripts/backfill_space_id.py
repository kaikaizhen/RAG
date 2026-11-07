"""
Phase 3：為既有 Qdrant point 寫入 space_id payload（依 MySQL Questions.VectorID → SpaceID 對應）。

對應檔（TSV，每行「vector_id<TAB>space_id」）可由 MySQL 匯出：
    SELECT VectorID, SpaceID FROM Questions WHERE VectorID IS NOT NULL AND VectorID <> '';

用法（連線沿用 QDRANT_HOST / QDRANT_PORT / QDRANT_COLLECTION，或 QDRANT_LOCATION）：
    python scripts/backfill_space_id.py mapping.tsv             # dry-run：只輸出對帳報告
    python scripts/backfill_space_id.py mapping.tsv --apply     # 寫入 space_id（有衝突時中止）
    python scripts/backfill_space_id.py mapping.tsv --rollback  # 移除對應 point 的 space_id

不會刪除任何 point；孤兒 point（Qdrant 有、MySQL 無）只列入報告，因缺少 space_id 而對所有 Space 不可見。
"""
import argparse
import os
import sys
from collections import defaultdict

from qdrant_client import QdrantClient
from qdrant_client.http import models

SPACE_ID_KEY = "space_id"


def connect() -> QdrantClient:
    location = os.getenv("QDRANT_LOCATION", "")
    if location:
        return QdrantClient(location=location)
    return QdrantClient(host=os.getenv("QDRANT_HOST", "localhost"), port=int(os.getenv("QDRANT_PORT", "6333")))


def read_mapping(path: str) -> dict:
    mapping = {}
    with open(path, encoding="utf-8") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            parts = line.split("\t")
            if len(parts) != 2 or not parts[1].isdigit() or int(parts[1]) <= 0:
                raise ValueError(f"第 {line_no} 行格式錯誤：{line!r}")
            vector_id, space_id = parts[0], int(parts[1])
            if mapping.get(vector_id, space_id) != space_id:
                raise ValueError(f"vector {vector_id} 對應到多個 space")
            mapping[vector_id] = space_id
    return mapping


def all_points(client: QdrantClient, collection: str) -> dict:
    points, offset = {}, None
    while True:
        batch, offset = client.scroll(collection, limit=256, offset=offset, with_payload=True, with_vectors=False)
        for p in batch:
            points[str(p.id)] = (p.payload or {}).get(SPACE_ID_KEY)
        if offset is None:
            return points


def reconcile(mapping: dict, points: dict) -> dict:
    return {
        "mapped_present": sorted(v for v in mapping if v in points),
        "missing_in_qdrant": sorted(v for v in mapping if v not in points),
        "orphans_in_qdrant": sorted(p for p in points if p not in mapping),
        "conflicts": sorted(v for v in mapping if v in points and points[v] is not None and points[v] != mapping[v]),
        "already_scoped": sorted(v for v in mapping if v in points and points[v] == mapping[v]),
    }


def print_report(title: str, report: dict, points: dict):
    print(f"== {title} ==")
    print(f"qdrant_points={len(points)}")
    for key, values in report.items():
        print(f"{key}={len(values)}" + (f" {values[:10]}" if values and key != "mapped_present" and key != "already_scoped" else ""))
    per_space = defaultdict(int)
    for space_id in points.values():
        per_space[space_id] += 1
    print("points_by_space=" + ", ".join(f"{k}:{v}" for k, v in sorted(per_space.items(), key=lambda kv: (kv[0] is None, kv[0] or 0))))


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("mapping", help="vector_id<TAB>space_id 對應檔")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--rollback", action="store_true")
    args = parser.parse_args(argv)

    collection = os.getenv("QDRANT_COLLECTION", "rag_knowledge")
    client = connect()
    mapping = read_mapping(args.mapping)
    points = all_points(client, collection)
    report = reconcile(mapping, points)
    print_report("before", report, points)

    if args.rollback:
        ids = report["mapped_present"]
        if ids:
            client.delete_payload(collection, keys=[SPACE_ID_KEY], points=ids)
        print(f"rolled_back={len(ids)}")
    elif args.apply:
        if report["conflicts"]:
            print("中止：部分 point 已屬於其他 space，需人工確認", file=sys.stderr)
            return 2
        by_space = defaultdict(list)
        for vector_id in report["mapped_present"]:
            by_space[mapping[vector_id]].append(vector_id)
        for space_id, ids in by_space.items():
            client.set_payload(collection, payload={SPACE_ID_KEY: space_id}, points=ids)
        client.create_payload_index(collection, field_name=SPACE_ID_KEY, field_schema=models.PayloadSchemaType.INTEGER)
        print(f"applied={len(report['mapped_present'])}")
    else:
        print("dry-run：未寫入任何資料")
        return 0

    after = all_points(client, collection)
    print_report("after", reconcile(mapping, after), after)
    return 0


if __name__ == "__main__":
    sys.exit(main())
