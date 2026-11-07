"""
回答是 Markdown，前端會渲染。回答流程（/ask）不可以破壞換行、縮排與程式碼區塊。
（過去的 dedup_text 會以「。」切句再接回去，吃掉換行、並在結尾硬加「。」。）
"""
import hashlib

import numpy as np
import pytest
from fastapi.testclient import TestClient

import main

KEY = {"X-Internal-Api-Key": "test-internal-key"}

DOC = """## 專案目錄結構

專案資料夾如下。
下一行緊接著上一行（句號後的換行必須保留）。

```text
專案
├── Commons
│   ├── Enums（資料夾）
│   └── Helpers（資料夾）
└── Controllers

    縮排的空行不可以把程式碼切成兩段
```

- 項目一。
- 項目二
    - 巢狀項目

結尾沒有句號"""


def fake_embed(text):
    seed = int(hashlib.sha256(text.encode("utf-8")).hexdigest()[:8], 16)
    return np.random.default_rng(seed).random(1024).tolist()


@pytest.fixture(autouse=True)
def fresh_collection(monkeypatch):
    monkeypatch.setattr(main, "embed_text", fake_embed)
    if main.COLLECTION_NAME in [c.name for c in main.client.get_collections().collections]:
        main.client.delete_collection(main.COLLECTION_NAME)
    main._space_index_ready = False
    yield


@pytest.fixture
def api():
    with TestClient(main.app) as c:
        yield c


def write(api, question, answer, qid):
    item = {"id": qid, "qaTypeId": 2, "qaItemTypeId": 3, "question": question, "answer": answer}
    response = api.post("/questionsToVector", json={"space_id": 1, "questions": [item]}, headers=KEY)
    assert response.status_code == 200


# ---------- 純函式 ----------

def test_split_blocks_keeps_a_fenced_code_block_whole_even_with_blank_lines_inside():
    blocks = main.split_blocks(DOC)

    code = [b for b in blocks if b.startswith("```text")]
    assert len(code) == 1
    assert "縮排的空行不可以把程式碼切成兩段" in code[0]
    assert code[0].rstrip().endswith("```")


def test_split_blocks_preserves_indentation_and_newlines_inside_a_block():
    block = main.split_blocks("第一行。\n第二行\n    縮排行")[0]

    assert block == "第一行。\n第二行\n    縮排行"


def test_dedup_blocks_removes_only_identical_paragraphs_and_keeps_their_content():
    text = "A 段落。\n接著一行\n\n\n\nB 段落\n\nA 段落。\n接著一行"

    assert main.dedup_blocks(text) == "A 段落。\n接著一行\n\nB 段落"


def test_dedup_blocks_does_not_touch_repeated_lines_inside_a_code_block():
    code = "```\nend\nend\nend\n```"

    assert main.dedup_blocks(code) == code


def test_truncate_cuts_only_at_block_boundaries_and_marks_the_cut():
    text = "第一段" * 10 + "\n\n" + "第二段" * 10 + "\n\n" + "第三段" * 10

    out = main.truncate_markdown(text, 70)

    assert out == "第一段" * 10 + "\n\n" + "第二段" * 10 + "\n\n…"


def test_truncate_does_nothing_when_short_enough():
    assert main.truncate_markdown("短短的\n\n兩段", 500) == "短短的\n\n兩段"


def test_truncate_never_cuts_through_a_fenced_block_in_the_middle_of_a_line_run():
    code = "```bash\n" + "\n".join(f"echo line{i}" for i in range(50)) + "\n```"

    out = main.truncate_markdown(code, 120)

    assert out.count("```") % 2 == 0  # 圍欄一定成對，後面的內容不會被整段當成程式碼
    assert out.endswith("…")
    assert len(out) < 200


def test_truncate_of_a_long_first_block_without_fence_cuts_on_a_line():
    text = "\n".join(f"第 {i} 行的內容" for i in range(100))

    out = main.truncate_markdown(text, 60)

    assert out.endswith("…")
    assert all(line.startswith("第 ") or line in ("", "…") for line in out.split("\n"))


# ---------- /ask ----------

def test_direct_match_returns_the_stored_answer_exactly(api):
    write(api, "專案目錄結構怎麼放？", DOC, 1)

    response = api.post("/ask", json={"space_id": 1, "question": "專案目錄結構怎麼放？"}, headers=KEY)

    assert response.status_code == 200
    body = response.json()
    assert body["method"] == "direct_match"
    assert body["answer"] == DOC  # 換行、縮排、程式碼區塊、「結尾沒有句號」都不變
    assert not body["answer"].endswith("。")


def test_crlf_answers_are_normalised_to_lf(api):
    write(api, "CRLF 問題", "第一行。\r\n第二行\r\n第三行", 1)

    answer = api.post("/ask", json={"space_id": 1, "question": "CRLF 問題"}, headers=KEY).json()["answer"]

    assert answer == "第一行。\n第二行\n第三行"


def test_summary_mode_separates_answers_with_blank_lines_and_dedups_identical_paragraphs(api, monkeypatch):
    monkeypatch.setattr(main, "DIRECT_MATCH_THRESHOLD", 2.0)  # 不可能達到 → 一律走摘要模式
    monkeypatch.setattr(main, "SIMILAR_SCORE_THRESHOLD", 0.0)
    monkeypatch.setattr(main, "SUMMARY_MAX_CHARS", 10_000)
    write(api, "問題一", "## 答案一\n\n- 清單 A。\n- 清單 B\n\n共用的說明段落", 1)
    write(api, "問題二", "## 答案二\n\n共用的說明段落", 2)

    body = api.post("/ask", json={"space_id": 1, "question": "問題一"}, headers=KEY).json()

    assert body["method"] == "context_summary"
    assert body["answer"].count("共用的說明段落") == 1  # 相同段落只留一次
    assert "- 清單 A。\n- 清單 B" in body["answer"]  # 清單的換行保留
    assert "\n\n## 答案二" in body["answer"] or "## 答案二\n\n" in body["answer"]  # 答案之間以空行分隔
    assert not body["answer"].endswith("。。")


def test_summary_mode_truncates_on_a_boundary_and_keeps_fences_balanced(api, monkeypatch):
    monkeypatch.setattr(main, "DIRECT_MATCH_THRESHOLD", 2.0)
    monkeypatch.setattr(main, "SIMILAR_SCORE_THRESHOLD", 0.0)
    monkeypatch.setattr(main, "SUMMARY_MAX_CHARS", 200)
    long_code = "```bash\n" + "\n".join(f"echo step{i}" for i in range(80)) + "\n```"
    write(api, "很長的指令", "說明文字。\n\n" + long_code, 1)

    body = api.post("/ask", json={"space_id": 1, "question": "很長的指令"}, headers=KEY).json()

    assert body["method"] == "context_summary"
    assert body["answer"].count("```") % 2 == 0
    assert body["answer"].rstrip().endswith("…")
    assert len(body["answer"]) < 400
