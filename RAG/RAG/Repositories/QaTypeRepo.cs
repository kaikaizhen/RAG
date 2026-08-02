using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QaTypeRepo : BaseRepo<WenDaHubContext, QaType>, IQaTypeRepo
    {
        public QaTypeRepo(WenDaHubContext context) : base(context) { }

        public async Task<QaType?> GetByIdAsync(int Id)
        {
            var query = QueryAsync(
                filter: q => q.ID == Id,
                includes:q=>q.Include(x=>x.QaItemType)
                             .ThenInclude(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<QaType>> GetAsync()
        {
            var query = QueryAsync(
                filter:q=>q.IsActive ==true
            );

            return await query.ToListAsync();
        }

        public async Task AddAsync(QaType entity)
        {
            await base.AddAsync(entity);
        }

        public async Task UpdateAsync(QaType entity)
        {
            await base.UpdateAsync(entity);
        }

        public async Task DeleteAsync(QaType entity)
        {
            await base.DeleteAsync(entity);
        }
    }
}
