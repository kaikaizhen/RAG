namespace RAG.Models.Params.Question
{
    public class CreateQuestionParam
    {
        /// <summary>
        /// 問題種類細項編號
        /// </summary>
        public required int QaItemTypeID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>
        public required string Question { get; set; }

        /// <summary>
        /// 解答
        /// </summary>
        public required string Answer { get; set; }

        /// <summary>
        /// 知識狀態：Verified / Stale / Inferred / Superseded（未提供時新增為 Verified、更新時保留原值）
        /// </summary>
        public string? Status { get; set; }
    }
}
