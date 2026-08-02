using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QaTypeItemRepo : BaseRepo<WenDaHubContext, QaItemType>, IQaTypeItemRepo
    {
        public QaTypeItemRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<QaItemType>> GetList()
        {
            var query = QueryAsync(
                filter: q => q.IsActive == true
            );

            return await query.ToListAsync();
        }

        public async Task<QaItemType?> GetByIdAsync(int Id)
        {
            var query = QueryAsync(
                filter: q => q.ID == Id,
                includes:q=>q.Include(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task AddAsync(QaItemType entity)
        {
            await base.AddAsync(entity);
        }

        public async Task UpdateAsync(QaItemType entity)
        {
            await base.UpdateAsync(entity);
        }

        public async Task DeleteAsync(QaItemType entity)
        {
            await base.DeleteAsync(entity);
        }

        public async Task DeleteRangeAsync(List<QaItemType> entities)
        {
            await base.DeleteRangeAsync(entities);
        }
    }
}
