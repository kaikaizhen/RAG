namespace RAG.Models.Params.QaType
{
    public class CreateQaTypeParam
    {
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
    }
}
