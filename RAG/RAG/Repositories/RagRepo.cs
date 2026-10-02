using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    /// <summary>
    /// 在 Library 的 BaseRepo 之上提供「寫入即存檔」與交易，維持服務層原有的寫入語意
    /// </summary>
    public abstract class RagRepo<TEntity> : BaseRepo<WenDaHubContext, TEntity>, IRagRepo<TEntity>
        where TEntity : class
    {
        private readonly WenDaHubContext _db;

        protected RagRepo(WenDaHubContext context) : base(context)
        {
            _db = context;
        }

        public async Task AddAsync(TEntity entity)
        {
            await base.AddAsync(entity);
            await SaveChangesAsync();
        }

        public async Task UpdateAsync(TEntity entity)
        {
            Update(entity);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(TEntity entity)
        {
            Remove(entity);
            await SaveChangesAsync();
        }

        public async Task DeleteRangeAsync(IEnumerable<TEntity> entities)
        {
            RemoveRange(entities);
            await SaveChangesAsync();
        }

        public async Task TransactionAsync()
        {
            await _db.Database.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            if (_db.Database.CurrentTransaction is not null)
            {
                await _db.Database.CommitTransactionAsync();
            }
        }
    }
}
