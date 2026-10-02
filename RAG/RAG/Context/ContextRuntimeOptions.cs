namespace RAG.Context
{
    /// <summary>
    /// Context Runtime 設定：平台不執行 LLM，只回傳受控的 Skill 內容與知識檢索證據
    /// </summary>
    public sealed class ContextRuntimeOptions
    {
        public const string SectionName = "ContextRuntime";

        /// <summary>
        /// 相似度達此分數才列為證據；與 RAG_Server 的 DIRECT_MATCH_THRESHOLD 為同一組值（由 compose/.env 注入）
        /// （依 qwen3-embedding:0.6b 校準：ambiguous 最高 0.662、direct match 0 false positive）
        /// </summary>
        public double EvidenceScoreThreshold { get; set; } = 0.70;

        /// <summary>
        /// 介於此分數與證據門檻之間者只列為「類似答案」（非證據）；與 RAG_Server 的 SIMILAR_SCORE_THRESHOLD 為同一組值
        /// （依 qwen3-embedding:0.6b 校準：查無答案最高 0.485、有答案最低 0.582）
        /// </summary>
        public double SimilarScoreThreshold { get; set; } = 0.50;

        /// <summary>
        /// 相對門檻：分數低於第一名超過此差距者，即使超過證據門檻也只列為類似答案
        /// （降低同主題但非所問的知識被當成證據；此為「相對第一名」的降級差距，與 RAG_Server 的 DIRECT_MATCH_MARGIN（top1 與 top2 的領先差距）語意不同，各自獨立設定）
        /// </summary>
        public double EvidenceRelativeMargin { get; set; } = 0.15;

        public int MaxEvidence { get; set; } = 5;

        public int MaxSimilar { get; set; } = 3;

        /// <summary>
        /// 向 RAG Server 取回的候選數（回查狀態後再篩選）
        /// </summary>
        public int SearchLimit { get; set; } = 20;

        public int DefaultTokenBudget { get; set; } = 8000;

        public int MaxTokenBudget { get; set; } = 32000;

        public void Validate()
        {
            if (!(SimilarScoreThreshold > 0 && SimilarScoreThreshold <= EvidenceScoreThreshold && EvidenceScoreThreshold <= 1))
                throw new InvalidOperationException("ContextRuntime: require 0 < SimilarScoreThreshold <= EvidenceScoreThreshold <= 1");
            if (EvidenceRelativeMargin is < 0 or > 1)
                throw new InvalidOperationException("ContextRuntime: EvidenceRelativeMargin must be 0-1");
            if (MaxEvidence <= 0 || MaxSimilar < 0 || SearchLimit is <= 0 or > 50)
                throw new InvalidOperationException("ContextRuntime: invalid MaxEvidence / MaxSimilar / SearchLimit");
            if (DefaultTokenBudget <= 0 || MaxTokenBudget < DefaultTokenBudget)
                throw new InvalidOperationException("ContextRuntime: invalid token budget");
        }
    }
}
