namespace RAG.Models.ViewModels.Chat
{
    public class ChatVM
    {
        /// <summary>
        /// 模型產生的最終回答
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// 相關問題（模型檢索出的類似問題）
        /// </summary>
        public List<string> Related { get; set; } = new List<string>();
    }
}
