"""
雙語（中 / 英 / 跨語言）知識檢索 embedding 評估。更換 EMBEDDING_MODEL 或調整 ContextRuntime 門檻前請重跑並比較：
Top-1 正確率、正確配對最低分、無關問句最高分與兩者區隔（區隔越大越容易設定證據 / 類似門檻）。

2026-09-25 結果（選定 BAAI/bge-m3，證據 0.6 / 類似 0.45 / 相對差距 0.15）：
  paraphrase-multilingual-MiniLM-L12-v2  Top-1 14/17  正確 min 0.163  無關 max 0.159  區隔 +0.004
  multilingual-e5-small                  Top-1 15/17  正確 min 0.817  無關 max 0.832  區隔 -0.015
  multilingual-e5-base                   Top-1 15/17  正確 min 0.802  無關 max 0.807  區隔 -0.005
  bge-m3                                 Top-1 17/17  正確 min 0.559  無關 max 0.428  區隔 +0.131

用法：python scripts/benchmark_embeddings.py [model ...]（首次執行會下載模型）
"""
import sys
import statistics
import time

import torch
from sentence_transformers import SentenceTransformer, util

DOCS = [
    ("zh", "員工要如何申請請假？請先在人資系統送出假單，由直屬主管核准後生效"),
    ("zh", "出差費用要怎麼報帳？於費用系統填寫報帳單並附上發票"),
    ("zh", "公司的 VPN 連線方式：安裝 WireGuard 並匯入 IT 提供的設定檔"),
    ("zh", "程式碼合併前需要至少兩位同事完成 Code Review"),
    ("en", "How to rotate API keys: generate a new key in the admin console, update the secret store, then revoke the old key"),
    ("en", "Database migrations must be rehearsed on a staging copy and have a tested rollback before production"),
    ("en", "When requirements are ambiguous, ask the human for clarification instead of guessing"),
    ("en", "Deployment checklist: run the full test suite, check health endpoints, and monitor error rates after release"),
]
# (期望的 doc index 或 None=無關, 問句, 類別)
CASES = [
    (0, "我想請假要怎麼做", "zh→zh"), (0, "請假流程", "zh→zh"), (1, "差旅費怎麼請款", "zh→zh"), (2, "遠端連線公司網路", "zh→zh"), (3, "PR 要幾個人審核", "zh→zh"),
    (4, "how do I change an API key", "en→en"), (5, "steps before running a schema migration in prod", "en→en"), (6, "what to do if the spec is unclear", "en→en"), (7, "what to verify after deploying", "en→en"),
    (4, "API 金鑰要怎麼更換", "zh→en"), (5, "資料庫遷移上線前要注意什麼", "zh→en"), (6, "需求不清楚時該怎麼辦", "zh→en"), (7, "上線後要檢查哪些項目", "zh→en"),
    (0, "how to request leave", "en→zh"), (1, "how to get travel expenses reimbursed", "en→zh"), (2, "how to connect to the company VPN", "en→zh"), (3, "how many reviewers are required before merging", "en→zh"),
    (None, "今天午餐吃什麼", "unrelated"), (None, "明天天氣如何", "unrelated"), (None, "股票會漲嗎", "unrelated"),
    (None, "what is the capital of France", "unrelated"), (None, "recommend a good movie", "unrelated"), (None, "how to bake bread", "unrelated"),
]
ALL_MODELS = [
    ("sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2", "", ""),
    ("intfloat/multilingual-e5-small", "query: ", "passage: "),
    ("intfloat/multilingual-e5-base", "query: ", "passage: "),
    ("BAAI/bge-m3", "", ""),
]

MODELS = [m for m in ALL_MODELS if not sys.argv[1:] or m[0] in sys.argv[1:]]

device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"device={device}")
for name, qp, dp in MODELS:
    t0 = time.time()
    model = SentenceTransformer(name, device=device)
    load = time.time() - t0
    docs = model.encode([dp + d for _, d in DOCS], normalize_embeddings=True)
    t1 = time.time()
    rows = []
    for exp, q, kind in CASES:
        sims = util.cos_sim(model.encode([qp + q], normalize_embeddings=True), docs)[0].tolist()
        best = max(range(len(DOCS)), key=lambda i: sims[i])
        rows.append((exp, kind, sims, best))
    per_query_ms = (time.time() - t1) / len(CASES) * 1000

    related = [r for r in rows if r[0] is not None]
    correct = [r[2][r[0]] for r in related]
    unrelated_max = [max(r[2]) for r in rows if r[0] is None]
    top1 = sum(r[3] == r[0] for r in related)
    by_kind = {}
    for exp, kind, sims, best in related:
        by_kind.setdefault(kind, []).append(best == exp)
    gap = min(correct) - max(unrelated_max)
    print(f"\n== {name.split('/')[-1]}  dim={docs.shape[1]}  load={load:.1f}s  encode={per_query_ms:.0f}ms/query")
    print("   Top-1: " + "  ".join(f"{k} {sum(v)}/{len(v)}" for k, v in by_kind.items()) + f"  | 合計 {top1}/{len(related)}")
    print(f"   正確配對分數 min={min(correct):.3f} median={statistics.median(correct):.3f}；無關最高={max(unrelated_max):.3f}；區隔(min正確-max無關)={gap:+.3f}")
    worst = sorted(((r[2][r[0]], CASES[i][1]) for i, r in enumerate(rows) if r[0] is not None))[:3]
    print("   最低的正確配對: " + "；".join(f"{q} {s:.3f}" for s, q in worst))
    del model
    if device == "cuda":
        torch.cuda.empty_cache()
