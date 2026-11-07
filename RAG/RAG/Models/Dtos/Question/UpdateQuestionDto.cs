namespace RAG.Models.Dtos.Question
{
    public class UpdateQuestionDto
    {
        /// <summary>
        /// 問題編號
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 問題種類細項編號
        /// </summary>
        public int QaItemTypeID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 解答
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// 更新時間
        /// </summary>
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 知識狀態：Verified / Stale / Inferred / Superseded（未提供時新增為 Verified、更新時保留原值）
        /// </summary>
        public string? Status { get; set; }
    }
}
