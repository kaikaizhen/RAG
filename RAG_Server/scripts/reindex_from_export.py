"""
以 MySQL（權威來源）匯出的知識，透過 RAG Server 的正常寫入路徑重建向量（例如更換 embedding 模型後的藍綠切換）。
point ID 沿用 Questions.VectorID，因此 MySQL 不需任何變更；重複執行結果相同（upsert）。

匯出（每行一個 JSON 物件）：
    SELECT JSON_OBJECT('id', q.ID, 'space_id', q.SpaceID, 'vector_id', q.VectorID, 'qa_type_id', i.QaTypeID,
                       'qa_item_type_id', q.QaItemTypeID, 'question', q.Question, 'answer', q.Answer)
    FROM Questions q JOIN QaItemType i ON i.ID = q.QaItemTypeID
    WHERE q.VectorID IS NOT NULL AND q.VectorID <> '';

用法（內部金鑰只從環境變數 INTERNAL_API_KEY 讀取）：
    python scripts/reindex_from_export.py export.jsonl --base-url http://127.0.0.1:8007            # dry-run
    python scripts/reindex_from_export.py export.jsonl --base-url http://127.0.0.1:8007 --apply    # 重建並核對
"""
import argparse
import json
import os
import sys
from collections import defaultdict
from typing import Callable, Dict, List

import httpx

REQUIRED = ("id", "space_id", "vector_id", "qa_type_id", "qa_item_type_id", "question", "answer")


def read_export(path: str) -> List[dict]:
    rows, seen = [], set()
    with open(path, encoding="utf-8") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            missing = [k for k in REQUIRED if row.get(k) in (None, "")]
            if missing:
                raise ValueError(f"第 {line_no} 行缺少欄位：{missing}")
            if not isinstance(row["space_id"], int) or row["space_id"] <= 0:
                raise ValueError(f"第 {line_no} 行 space_id 無效")
            if row["vector_id"] in seen:
                raise ValueError(f"第 {line_no} 行 vector_id 重複：{row['vector_id']}")
            seen.add(row["vector_id"])
            rows.append(row)
    return rows


def by_space(rows: List[dict]) -> Dict[int, List[dict]]:
    grouped = defaultdict(list)
    for row in rows:
        grouped[row["space_id"]].append(row)
    return dict(grouped)


def reconcile(rows: List[dict], get: Callable[[str], dict]) -> Dict[int, dict]:
    """逐 Space 比對：目標 collection 中的 point ID 必須與匯出完全一致"""
    report = {}
    for space_id, items in sorted(by_space(rows).items()):
        present = {str(q["id"]) for q in get(f"/questions?space_id={space_id}&limit=1000")["questions"]}
        expected = {r["vector_id"] for r in items}
        report[space_id] = {"expected": len(expected), "present": len(present),
                            "missing": sorted(expected - present), "extra": sorted(present - expected)}
    return report


def apply(rows: List[dict], post: Callable[[str, dict], dict], batch_size: int) -> int:
    written = 0
    for space_id, items in sorted(by_space(rows).items()):
        for start in range(0, len(items), batch_size):
            batch = items[start:start + batch_size]
            body = {"space_id": space_id, "questions": [
                {"id": r["id"], "referenceId": r["vector_id"], "qaTypeId": r["qa_type_id"],
                 "qaItemTypeId": r["qa_item_type_id"], "question": r["question"], "answer": r["answer"]}
                for r in batch]}
            result = post("/questionsToVector", body)
            if result.get("vectorIds") != [r["vector_id"] for r in batch]:
                raise RuntimeError(f"space {space_id}: 回傳的 vectorIds 與請求不一致")
            written += len(batch)
    return written


def http_client(base_url: str, key: str):
    client = httpx.Client(base_url=base_url.rstrip("/"), headers={"X-Internal-Api-Key": key}, timeout=300)

    def post(path, body):
        r = client.post(path, json=body)
        r.raise_for_status()
        return r.json()

    def get(path):
        r = client.get(path)
        r.raise_for_status()
        return r.json()

    return post, get


def main(argv=None, post=None, get=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("export")
    parser.add_argument("--base-url", default="http://127.0.0.1:8007")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--batch-size", type=int, default=32)
    args = parser.parse_args(argv)

    rows = read_export(args.export)
    spaces = by_space(rows)
    print(f"rows={len(rows)} spaces=" + ", ".join(f"{s}:{len(v)}" for s, v in sorted(spaces.items())))

    if post is None or get is None:
        key = os.getenv("INTERNAL_API_KEY", "")
        if not key:
            print("INTERNAL_API_KEY 未設定", file=sys.stderr)
            return 2
        post, get = http_client(args.base_url, key)

    if not args.apply:
        print("dry-run：未寫入任何資料")
        return 0

    print(f"written={apply(rows, post, args.batch_size)}")
    report = reconcile(rows, get)
    ok = True
    for space_id, r in report.items():
        print(f"space {space_id}: expected={r['expected']} present={r['present']} missing={len(r['missing'])} extra={len(r['extra'])}")
        ok &= not r["missing"] and not r["extra"]
    print("reconciliation=" + ("OK" if ok else "MISMATCH"))
    return 0 if ok else 3


if __name__ == "__main__":
    sys.exit(main())
