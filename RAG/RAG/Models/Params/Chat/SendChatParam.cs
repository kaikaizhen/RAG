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
        /// 聊天編號
        /// </summary>
        public string? ChatID {  get; set; }
    }
}
