namespace RAG.Models.ViewModels.QaTypeItem
{
    public class GetQaTypeItemVM
    {
        /// <summary>
        /// 問題種類細項編號
        /// </summary>
        public int ID { get; set; }

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
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// 問題種類細項名稱(其他語系)
        /// </summary>
        public string NameOther { get; set; } = string.Empty;

        /// <summary>
        /// 是否啟用
        /// </summary>
        public bool IsActive { get; set; }
    }
}
