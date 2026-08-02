namespace RAG.Models.Params.Question
{
    public class UpdateQuestionParam
    {
        /// <summary>
        /// 問題編號
        /// </summary>
        public required int ID { get; set; }

        /// <summary>
        /// 問題種類細項編號
        /// </summary>
        public required int QaItemTypeID { get; set; }

        /// <summary>
        /// 創建者編號
        /// </summary>
        public required int UserID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>
        public required string Question { get; set; }

        /// <summary>
        /// 解答
        /// </summary>
        public required string Answer { get; set; }
    }
}
