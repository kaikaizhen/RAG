using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QaTypeItemRepo : RagRepo<QaItemType>, IQaTypeItemRepo
    {
        public QaTypeItemRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<QaItemType>> GetList()
        {
            var query = Query(
                filter: q => q.IsActive == true
            );

            return await query.ToListAsync();
        }

        public async Task<QaItemType?> GetByIdAsync(int Id)
        {
            var query = Query(
                filter: q => q.ID == Id,
                includes:q=>q.Include(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }
    }
}
