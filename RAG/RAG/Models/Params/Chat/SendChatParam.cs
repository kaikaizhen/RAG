namespace RAG.Models.Params.Chat
{
    public class SendChatParam
    {
        /// <summary>
        /// 問題描述
        /// </summary>
        public required string Question {  get; set; }

        /// <summary>
        /// 搜尋範圍
        /// </summary>
        public string? Type { get; set; } = null;

        /// <summary>
        /// 檢索範圍：current（只找目前的空間，預設）或 all（我所有可讀的空間）。實際範圍由伺服器依你的 membership 決定
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// 聊天編號
        /// </summary>
        public string? ChatID {  get; set; }
    }
}
