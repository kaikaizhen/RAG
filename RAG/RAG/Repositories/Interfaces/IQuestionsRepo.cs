using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQuestionsRepo:IBaseRepo<WenDaHubContext,Questions>
    {
        /// <summary>
        /// 查詢問題列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<List<Questions>> Get(int userId);

        /// <summary>
        /// 查詢問題列表(根據問題種類)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<List<Questions>> GetByTypeIdAsync(int userId, int Id);

        /// <summary>
        /// 查詢問題(僅限擁有者)
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="userId">擁有者使用者編號</param>
        /// <returns></returns>
        Task<Questions?> GetByIdAsync(int Id, int userId);

        /// <summary>
        /// 新增問題
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task CreateAsync(Questions entity);

        /// <summary>
        /// 更新問題
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task UpdateAsync(Questions entity);

        /// <summary>
        /// 刪除問題
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(Questions entity);

        /// <summary>
        /// 刪除多個問題
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        Task DeleteRangeAsync(List<Questions> entities);
    }
}
