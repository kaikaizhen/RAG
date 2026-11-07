using Library.Repo.Interfaces;
using RAG.Models.Entities;

namespace RAG.Repositories.Interfaces
{
    /// <summary>
    /// Library 的 IBaseRepo 只追蹤變更、不存檔；本專案的服務層以「每次寫入即 SaveChanges」為既有行為，
    /// 並以 TransactionAsync / CommitAsync 圈住多筆寫入，因此在此明確宣告，避免誤呼叫 Library 的不存檔版本。
    /// </summary>
    public interface IRagRepo<TEntity> : IBaseRepo<WenDaHubContext, TEntity> where TEntity : class
    {
        /// <summary>
        /// 新增並立即存檔
        /// </summary>
        Task AddAsync(TEntity entity);

        /// <summary>
        /// 更新並立即存檔
        /// </summary>
        Task UpdateAsync(TEntity entity);

        /// <summary>
        /// 刪除並立即存檔
        /// </summary>
        Task DeleteAsync(TEntity entity);

        /// <summary>
        /// 刪除多筆並立即存檔
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// 開始交易
        /// </summary>
        Task TransactionAsync();

        /// <summary>
        /// 提交目前交易（沒有交易時不做事）
        /// </summary>
        Task CommitAsync();
    }
}
