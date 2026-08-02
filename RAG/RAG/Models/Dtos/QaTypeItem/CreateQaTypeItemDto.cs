namespace RAG.Models.Dtos.QaTypeItem
{
    public class CreateQaTypeItemDto
    {
        /// <summary>
        /// 問題種類編號
        /// </summary>
        public int QaTypeID { get; set; }

        /// <summary>
        /// 問題種類細項名稱(中文)
        /// </summary>
        public string NameZh { get; set; } = string.Empty;

        /// <summary>
        /// 問題種類細項名稱(英文)
        /// </summary>
        public string NameEn { get; set; }= string.Empty;

        /// <summary>
        /// 問題種類細項名稱(其他語系)
        /// </summary>

        public string NameOther { get; set; } = string.Empty;

        /// <summary>
        /// 啟用狀態(預設：啟用)
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
