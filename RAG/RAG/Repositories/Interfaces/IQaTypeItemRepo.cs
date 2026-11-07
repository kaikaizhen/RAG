using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQaTypeItemRepo:IRagRepo<QaItemType>
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
    }
}
