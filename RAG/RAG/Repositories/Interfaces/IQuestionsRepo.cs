using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQuestionsRepo:IBaseRepo<WenDaHubContext,Questions>
    {
        /// <summary>
        /// 查詢問題列表(擁有者 + Space)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="spaceId"></param>
        /// <returns></returns>
        Task<List<Questions>> Get(int userId, int spaceId);

        /// <summary>
        /// 查詢問題列表(根據問題種類；擁有者 + Space)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="spaceId"></param>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<List<Questions>> GetByTypeIdAsync(int userId, int spaceId, int Id);

        /// <summary>
        /// 查詢問題(僅限擁有者且屬於指定 Space)
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="userId">擁有者使用者編號</param>
        /// <param name="spaceId">Space 編號</param>
        /// <returns></returns>
        Task<Questions?> GetByIdAsync(int Id, int userId, int spaceId);

        /// <summary>
        /// 是否有任何 Space 的問題使用這些問題種類細項（用於阻止跨 Space 連帶刪除）
        /// </summary>
        /// <param name="qaItemTypeIds"></param>
        /// <returns></returns>
        Task<bool> AnyByQaItemTypeIdsAsync(IEnumerable<int> qaItemTypeIds);

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
