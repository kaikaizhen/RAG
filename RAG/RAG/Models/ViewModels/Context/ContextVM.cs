namespace RAG.Models.ViewModels.Context
{
    public class KnowledgeSearchParam
    {
        /// <summary>
        /// 查詢內容（1-2000 字元）
        /// </summary>
        public required string Query { get; set; }

        /// <summary>
        /// 問題種類編號（選填，限縮搜尋範圍）
        /// </summary>
        public string? Type { get; set; }
    }

    public class KnowledgeSearchVM
    {
        /// <summary>
        /// Space 編號
        /// </summary>
        public int SpaceID { get; set; }

        /// <summary>
        /// Space 識別字
        /// </summary>
        public string SpaceKey { get; set; } = string.Empty;

        /// <summary>
        /// grounded：有 Verified 證據；partial：只有 Stale / Inferred 證據；insufficient：沒有證據
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 可作為依據的知識（依分數排序；Stale / Inferred 可信度較低）
        /// </summary>
        public List<KnowledgeItemVM> Evidence { get; set; } = new();

        /// <summary>
        /// 類似但分數不足的答案：僅供參考，不可當作依據
        /// </summary>
        public List<KnowledgeItemVM> Similar { get; set; } = new();

        /// <summary>
        /// 給使用端 AI 的使用說明
        /// </summary>
        public string Notice { get; set; } = string.Empty;
    }

    public class KnowledgeItemVM
    {
        /// <summary>
        /// 知識（問題）編號
        /// </summary>
        public int QuestionID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 答案
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// 相似度分數（0-1）
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 知識狀態：Verified / Stale / Inferred
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    public class SkillBundleVM
    {
        /// <summary>
        /// Space 識別字
        /// </summary>
        public string SpaceKey { get; set; } = string.Empty;

        /// <summary>
        /// 已載入的 Skill（依順序：Default 在前、Space 擴充在後）
        /// </summary>
        public List<LoadedArtifactVM> Skills { get; set; } = new();

        /// <summary>
        /// 已載入的 Workflow（未設定或超過預算時為 null）
        /// </summary>
        public LoadedArtifactVM? Workflow { get; set; }

        /// <summary>
        /// 因 token 預算不足未載入的 Skill
        /// </summary>
        public List<string> OmittedSkillIds { get; set; } = new();

        /// <summary>
        /// 已載入內容的預估 token 數
        /// </summary>
        public int TokenEstimate { get; set; }

        /// <summary>
        /// 本次使用的 token 預算
        /// </summary>
        public int TokenBudget { get; set; }
    }

    public class LoadedArtifactVM
    {
        /// <summary>
        /// 識別字
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 來源：default 或 space:&lt;key&gt;
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 內容（Markdown）
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 預估 token 數
        /// </summary>
        public int TokenEstimate { get; set; }
    }
}
