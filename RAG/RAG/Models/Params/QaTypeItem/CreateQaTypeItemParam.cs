namespace RAG.Models.Params.QaTypeItem
{
    public class CreateQaTypeItemParam
    {
        /// <summary>
        /// 問題種類編號
        /// </summary>
        public required int QaTypeID { get; set; }

        /// <summary>
        /// 問題種類細項名稱(中文)
        /// </summary>
        public string? NameZh { get; set; }

        /// <summary>
        /// 問題種類細項名稱(英文)
        /// </summary>
        public string? NameEn { get; set; }

        /// <summary>
        /// 問題種類細項名稱(其他語系)
        /// </summary>
        public string? NameOther { get; set; }
    }
}
