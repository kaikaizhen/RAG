namespace RAG.Models.Dtos.Chat
{
    public class GetChatHistoryDto
    {
        /// <summary>
        /// 聊天室編號
        /// </summary>
        public string ID { get; set; } = string.Empty;

        /// <summary>
        /// 聊天室名稱
        /// </summary>
        public string Name {  get; set; } = string.Empty;
    }
}
