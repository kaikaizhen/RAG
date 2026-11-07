using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QaTypeRepo : RagRepo<QaType>, IQaTypeRepo
    {
        public QaTypeRepo(WenDaHubContext context) : base(context) { }

        public async Task<QaType?> GetByIdAsync(int Id)
        {
            var query = Query(
                filter: q => q.ID == Id,
                includes:q=>q.Include(x=>x.QaItemType)
                             .ThenInclude(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<QaType>> GetAsync()
        {
            var query = Query(
                filter:q=>q.IsActive ==true
            );

            return await query.ToListAsync();
        }
    }
}
