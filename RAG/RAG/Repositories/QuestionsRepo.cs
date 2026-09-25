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

        public async Task<List<Questions>> Get(int userId, int spaceId)
        {
            var query = QueryAsync(
                 filter: q => q.UserID == userId && q.SpaceID == spaceId
            );

            return await query.ToListAsync();
        }

        public async Task<List<Questions>> GetByTypeIdAsync(int userId, int spaceId, int Id)
        {
            var query = QueryAsync(
                filter: q=>q.UserID == userId && q.SpaceID == spaceId && q.QaItemTypeID == Id
            );

            return await query.ToListAsync();
        }

        public async Task<Questions?> GetByIdAsync(int Id, int userId, int spaceId)
        {
            var query = QueryAsync(
               filter: q => q.ID == Id && q.UserID == userId && q.SpaceID == spaceId,
               includes:q=>q.Include(x=>x.QaItemType.QaType)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<bool> AnyByQaItemTypeIdsAsync(IEnumerable<int> qaItemTypeIds)
        {
            var ids = qaItemTypeIds.ToList();
            return await QueryAsync(filter: q => ids.Contains(q.QaItemTypeID)).AnyAsync();
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
