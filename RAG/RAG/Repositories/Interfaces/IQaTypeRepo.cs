using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQaTypeRepo:IBaseRepo<WenDaHubContext,QaType>
    {
        /// <summary>
        /// 查詢問題種類
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        Task<QaType?> GetByIdAsync(int Id);

        /// <summary>
        /// 查詢問題種類列表
        /// </summary>
        /// <returns></returns>
        Task<List<QaType>> GetAsync();

        /// <summary>
        /// 新增問題種類
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task AddAsync(QaType entity);

        /// <summary>
        /// 更新問題種類
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task UpdateAsync(QaType entity);

        /// <summary>
        /// 刪除問題種類
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(QaType entity);
    }
}
