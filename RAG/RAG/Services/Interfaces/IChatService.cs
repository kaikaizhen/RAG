using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;

namespace RAG.Services.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// 發送回應
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task<ChatReponseDto> SendChatAsync(ChatDto dto);

        /// <summary>
        /// 查詢目前使用者的聊天室清單
        /// </summary>
        /// <returns></returns>
        Task<List<GetChatHistoryDto>> GetChatHistoryAsync();

        /// <summary>
        /// 查詢聊天室記錄清單
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<List<GetChatLogHistoryDto>> GetChatLogHistoryAsync(string Id);

        /// <summary>
        /// 刪除聊天室(含紀錄)
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task DeleteChatHistoryAsync(string Id);
    }
}
