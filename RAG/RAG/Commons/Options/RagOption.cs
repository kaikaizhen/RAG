namespace RAG.Commons.Options
{
    public class RagOption
    {
        /// <summary>
        /// 組態區段名稱
        /// </summary>
        public const string SectionName = "RagOption";

        /// <summary>
        /// 基本連線Url（請於 appsettings 或環境變數設定）
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// 轉換向量
        /// </summary>
        public string ToVectorUrl { get; set; } = "/questionsToVector";

        /// <summary>
        /// 回應
        /// </summary>
        public string AskUrl { get; set; } = "/ask";

        /// <summary>
        /// 刪除問題
        /// </summary>
        public string DeleteUrl { get; set; } = "/questions/";

        /// <summary>
        /// 刪除種類向量
        /// </summary>
        public string DeleteTypeUrl { get; set; } = "/delete_type/";

        /// <summary>
        /// 查詢目前向量資料庫種類
        /// </summary>
        public string CollectionUrl { get; set; } = "/collections";
    }
}
