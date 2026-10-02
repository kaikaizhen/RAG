using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    public interface IQuestionsRepo:IRagRepo<Questions>
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
        /// 取得指定 Space 內的多筆問題（知識檢索後回查權威內容與狀態；不限建立者）
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="spaceId"></param>
        /// <returns></returns>
        Task<List<Questions>> GetByIdsInSpaceAsync(IEnumerable<int> ids, int spaceId);

        /// <summary>
        /// 是否有任何 Space 的問題使用這些問題種類細項（用於阻止跨 Space 連帶刪除）
        /// </summary>
        /// <param name="qaItemTypeIds"></param>
        /// <returns></returns>
        Task<bool> AnyByQaItemTypeIdsAsync(IEnumerable<int> qaItemTypeIds);
    }
}
