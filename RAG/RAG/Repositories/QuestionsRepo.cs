using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Dtos.Question;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QuestionsRepo : BaseRepo<WenDaHubContext,Questions>,IQuestionsRepo
    {
        public QuestionsRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<Questions>> Get(int userId)
        {
            var query = QueryAsync(
                 filter: q => q.UserID == userId
            );

            return await query.ToListAsync();
        }

        public async Task<List<Questions>> GetByTypeIdAsync(int userId,int Id)
        {
            var query = QueryAsync(
                filter: q=>q.UserID == userId && q.QaItemTypeID == Id  
            );

            return await query.ToListAsync();
        }

        public async Task<Questions?> GetByIdAsync(int Id, int userId)
        {
            var query = QueryAsync(
               filter: q => q.ID == Id && q.UserID == userId,
               includes:q=>q.Include(x=>x.QaItemType.QaType)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task CreateAsync(Questions entity)
        {
            await AddAsync(entity);
        }

        public async Task UpdateAsync(Questions entity)
        {
            await base.UpdateAsync(entity);
        }

        public async Task DeleteAsync(Questions entity)
        {
            await base.DeleteAsync(entity);
        }

        public async Task DeleteRangeAsync(List<Questions> entities)
        {
            await base.DeleteRangeAsync(entities);
        }
    }
}
