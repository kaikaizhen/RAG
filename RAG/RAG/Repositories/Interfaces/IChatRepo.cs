using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IChatRepo:IBaseRepo<WenDaHubContext,ChatHeader>
    {
        /// <summary>
        /// 查詢聊天資訊(僅限擁有者)
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="userId">擁有者使用者編號</param>
        /// <returns></returns>
        Task<ChatHeader?> GetByIdAsync(int Id, int userId);

        /// <summary>
        /// 查詢聊天清單(根據使用者編號)
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<List<ChatHeader>> GetByUserIdAsync(int Id);

        /// <summary>
        /// 新增聊天資訊
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task AddAsync(ChatHeader entity);

        /// <summary>
        /// 刪除聊天資訊
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(ChatHeader entity);
    }
}
