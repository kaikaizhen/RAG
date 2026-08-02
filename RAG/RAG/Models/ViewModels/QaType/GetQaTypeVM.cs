namespace RAG.Models.ViewModels.QaType
{
    public class GetQaTypeVM
    {
        /// <summary>
        /// 問題種類編號
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 種類名稱(中文)
        /// </summary>
        public string NameZh { get; set; } = string.Empty;

        /// <summary>
        /// 種類名稱(英文)
        /// </summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// 種類名稱(其他語系)
        /// </summary>
        public string NameOther { get; set; } = string.Empty;
    }
}
