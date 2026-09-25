using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IChatLogRepo:IBaseRepo<WenDaHubContext, ChatLog>
    {
        /// <summary>
        /// 查詢聊天室記錄清單(僅限聊天室擁有者且屬於指定 Space)
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="userId">擁有者使用者編號</param>
        /// <param name="spaceId">Space 編號</param>
        /// <returns></returns>
        Task<List<ChatLog>> GetAsync(int Id, int userId, int spaceId);

        /// <summary>
        /// 刪除聊天室紀錄清單
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        Task DeleteRangeAsync(List<ChatLog> entities);
    }
}
