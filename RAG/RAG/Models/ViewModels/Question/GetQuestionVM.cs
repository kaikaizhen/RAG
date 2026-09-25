namespace RAG.Models.ViewModels.Question
{
    public class GetQuestionVM
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
        /// 創建者編號
        /// </summary>
        public int UserID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>

        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 解答
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// 創建時間
        /// </summary>
        public DateTime CreateAt { get; set; }

        /// <summary>
        /// 更新時間
        /// </summary>
        public DateTime? UpdateAt { get; set; }

        /// <summary>
        /// 知識狀態
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
