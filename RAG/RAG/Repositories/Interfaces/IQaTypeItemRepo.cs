using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQaTypeItemRepo:IBaseRepo<WenDaHubContext,QaItemType>
    {
        /// <summary>
        /// 查詢問題種類細項列表
        /// </summary>
        /// <returns></returns>
        Task<List<QaItemType>> GetList();

        /// <summary>
        /// 查詢問題種類細項
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<QaItemType?> GetByIdAsync(int Id);

        /// <summary>
        /// 新增問題種類細項
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task AddAsync(QaItemType entity);

        /// <summary>
        /// 更新問題種類細項
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task UpdateAsync(QaItemType entity);

        /// <summary>
        /// 刪除問題種類細項
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(QaItemType entity);

        /// <summary>
        /// 刪除多個問題種類細項
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        Task DeleteRangeAsync(List<QaItemType> entities);
    }
}
