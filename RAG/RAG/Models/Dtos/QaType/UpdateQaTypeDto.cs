namespace RAG.Models.Dtos.QaType
{
    public class UpdateQaTypeDto
    {
        /// <summary>
        /// 問題種類編號
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 問題種類名稱(中文)
        /// </summary>
        public string? NameZh { get; set; }

        /// <summary>
        /// 問題種類名稱(英文)
        /// </summary>
        public string? NameEn { get; set; }

        /// <summary>
        /// 問題種類名稱(其他語系)
        /// </summary>
        public string? NameOther { get; set; }

        /// <summary>
        /// 啟用狀態
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
