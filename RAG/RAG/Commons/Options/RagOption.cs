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

        public string SearchUrl { get; set; } = "/search";

        /// <summary>
        /// 刪除問題
        /// </summary>
        public string DeleteUrl { get; set; } = "/questions/";

        /// <summary>
        /// 刪除種類向量
        /// </summary>
        /// <summary>
        /// 呼叫 RAG Server 的內部金鑰（X-Internal-Api-Key）；僅由環境變數或未納版控的設定檔提供
        /// </summary>
        public string InternalApiKey { get; set; } = string.Empty;

        /// <summary>
        /// 公開知識的 Space；所有人問答時，自己的 Space 答不出來才查這裡。0 表示不啟用。只由伺服器設定決定
        /// </summary>
        public int PublicSpaceId { get; set; }

        /// <summary>
        /// 查詢目前向量資料庫種類
        /// </summary>
        public string CollectionUrl { get; set; } = "/collections";
    }
}
