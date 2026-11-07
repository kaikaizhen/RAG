"""匯出 rag-ai 的 OpenAPI 3 與 Postman Collection（docs/）。

用法（在 RAG_Server 目錄）：python scripts/export_openapi.py
輸出只含結構與範例，不含任何金鑰；金鑰在 Postman 的變數 internalApiKey 填入。
"""
import json
import os
import sys
from pathlib import Path

os.environ.setdefault("QDRANT_LOCATION", ":memory:")
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from main import app  # noqa: E402

OUT = Path(__file__).resolve().parent.parent / "docs"
PUBLIC_URL = "https://rag.name404.com/ai"
KEY_HEADER = "X-Internal-Api-Key"

EXAMPLES = {
    ("post", "/ask"): {"space_id": 1, "question": "什麼是三層式架構？", "type": None},
    ("post", "/search"): {"space_id": 1, "query": "Docker 如何建置映像檔", "limit": 5, "type": None},
    ("post", "/questionsToVector"): {
        "space_id": 1,
        "questions": [{"id": 1, "referenceId": None, "qaTypeId": 1, "qaItemTypeId": 1, "question": "範例問題", "answer": "範例答案（支援 Markdown）"}],
    },
}
QUERY_DEFAULTS = {"space_id": "1", "limit": "100"}
PATH_DEFAULTS = {"question_id": "{{questionId}}"}


def build_openapi() -> dict:
    spec = app.openapi()
    spec["info"]["description"] = (
        "RAG 向量服務（內部服務）。除 `GET /` 外，所有端點都需要 `X-Internal-Api-Key` 標頭，"
        "由 .NET API 與本服務共用；請勿對外公開。"
    )
    spec["servers"] = [{"url": PUBLIC_URL, "description": "NAS（經 Nginx /ai/）"}, {"url": "http://localhost:8007", "description": "本機"}]
    spec.setdefault("components", {}).setdefault("securitySchemes", {})["InternalKey"] = {"type": "apiKey", "in": "header", "name": KEY_HEADER}
    for path, ops in spec["paths"].items():
        for method, op in ops.items():
            if path != "/":
                op["security"] = [{"InternalKey": []}]
                # 金鑰改由 security scheme 表達，不再重複成每個操作的 header 參數
                op["parameters"] = [p for p in op.get("parameters", []) if p.get("name", "").lower().replace("_", "-") != KEY_HEADER.lower()]
                if not op["parameters"]:
                    op.pop("parameters")
            example = EXAMPLES.get((method, path))
            if example is not None:
                op["requestBody"]["content"]["application/json"]["example"] = example
    return spec


def build_postman(spec: dict) -> dict:
    items = []
    for path, ops in spec["paths"].items():
        for method, op in ops.items():
            url_path = [seg for seg in path.strip("/").split("/") if seg]
            url_path = [PATH_DEFAULTS.get(s.strip("{}"), s) if s.startswith("{") else s for s in url_path]
            query = [
                {"key": p["name"], "value": QUERY_DEFAULTS.get(p["name"], "")}
                for p in op.get("parameters", []) if p["in"] == "query"
            ]
            headers = [] if path == "/" else [{"key": KEY_HEADER, "value": "{{internalApiKey}}", "type": "text"}]
            request = {
                "method": method.upper(),
                "header": headers,
                "url": {"raw": "{{baseUrl}}/" + "/".join(url_path) + ("?" + "&".join(f"{q['key']}={q['value']}" for q in query) if query else ""),
                        "host": ["{{baseUrl}}"], "path": url_path, "query": query},
                "description": op.get("summary", ""),
            }
            example = EXAMPLES.get((method, path))
            if example is not None:
                headers.append({"key": "Content-Type", "value": "application/json", "type": "text"})
                request["body"] = {"mode": "raw", "raw": json.dumps(example, ensure_ascii=False, indent=2), "options": {"raw": {"language": "json"}}}
            items.append({"name": f"{method.upper()} {path}", "request": request})
    return {
        "info": {
            "name": "RAG AI（向量服務）",
            "description": "匯入後到 Collection 的 Variables 填入 internalApiKey；baseUrl 預設為 NAS，本機測試改成 http://localhost:8007。",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
        },
        "variable": [
            {"key": "baseUrl", "value": PUBLIC_URL},
            {"key": "internalApiKey", "value": ""},
            {"key": "questionId", "value": ""},
        ],
        "item": items,
    }


if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    spec = build_openapi()
    (OUT / "rag-ai.openapi.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (OUT / "rag-ai.postman_collection.json").write_text(json.dumps(build_postman(spec), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("paths:", list(spec["paths"]))
