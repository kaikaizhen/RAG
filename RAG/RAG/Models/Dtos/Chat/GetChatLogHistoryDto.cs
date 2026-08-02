namespace RAG.Models.Dtos.Chat
{
    public class GetChatLogHistoryDto
    {
        /// <summary>
        /// 問題
        /// </summary>
        public string Questions { get; set; } =string.Empty;

        /// <summary>
        /// 回答
        /// </summary>
        public string Answer { get; set; } =string.Empty;

        /// <summary>
        /// 創建時間
        /// </summary>
        public DateTime CreateAt { get; set; }
    }
}
