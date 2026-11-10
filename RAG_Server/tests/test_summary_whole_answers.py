"""
摘要模式以整篇答案為單位：不會把一篇規則切成兩半，放不下的答案留下來源標記，前端會顯示成可點的來源標籤。
"""
import main

M1 = "來源：learning-notes rule#global-system／BTOOption／正式寫入點"
M2 = "來源：learning-notes rule#global-system／BTOOption／五個步驟"
M3 = "來源：learning-notes concept#7"


def doc(title, body, marker):
    return f"## {title}\n規則：x　可信度：從程式推測\n\n{body}\n\n{marker}"


def test_whole_answers_fit_and_keep_every_marker():
    text, omitted = main.summarize_answers([doc("一", "a" * 50, M1), doc("二", "b" * 50, M2)], 2000)
    assert not omitted
    assert text.count("## ") == 2
    assert M1 in text and M2 in text


def test_answer_that_does_not_fit_is_dropped_but_its_marker_stays():
    text, omitted = main.summarize_answers([doc("一", "a" * 120, M1), doc("二", "b" * 120, M2), doc("三", "c" * 120, M3)], 200)
    assert omitted
    assert "## 一" in text
    assert "## 二" not in text and "bbbb" not in text  # 不會切成半篇
    assert M2 in text and M3 in text  # 還是看得到來源


def test_first_answer_longer_than_limit_is_truncated_on_a_boundary_with_marker():
    long = "\n\n".join(f"第 {i} 段內容。" for i in range(100))
    text, omitted = main.summarize_answers([doc("長", long, M1)], 120)
    assert omitted
    assert M1 in text
    assert "…" in text


def test_duplicate_paragraphs_across_answers_appear_once():
    text, _ = main.summarize_answers([doc("一", "共用段落", M1), doc("二", "共用段落", M2)], 2000)
    assert text.count("共用段落") == 1
    assert M2 in text
