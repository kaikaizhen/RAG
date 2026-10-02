using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IUserCacheRepo:IRagRepo<UserCache>
    {
        /// <summary>
        /// 查詢使用者(根據帳號密碼)
        /// </summary>
        /// <param name="account"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        Task<UserCache?> GetUserByAccountAndPasswordAsync(string account, string password);

        /// <summary>
        /// 查詢是否已經有該使用者帳號
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        Task<bool> IncludeAccountAsync(string account);

        /// <summary>
        /// 查詢使用者(根據唯一值)
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<UserCache?> GetByIdAsync(int Id);

    }
}
