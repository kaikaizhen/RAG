namespace RAG.Models.Dtos.Chat
{
    public class ChatDto
    {
        /// <summary>
        /// 問題描述
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 搜尋範圍
        /// </summary>
        public string? Type { get; set; } = null;

        /// <summary>
        /// 聊天編號
        /// </summary>
        public string ChatID { get; set; } = string.Empty;

        /// <summary>
        /// 使用者編號
        /// </summary>
        public  int UserID { get; set; }
    }
}
