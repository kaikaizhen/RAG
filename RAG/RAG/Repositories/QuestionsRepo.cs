using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Dtos.Question;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class QuestionsRepo : RagRepo<Questions>,IQuestionsRepo
    {
        public QuestionsRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<Questions>> Get(int userId, int spaceId)
        {
            var query = Query(
                 filter: q => q.UserID == userId && q.SpaceID == spaceId
            );

            return await query.ToListAsync();
        }

        public async Task<List<Questions>> GetByTypeIdAsync(int userId, int spaceId, int Id)
        {
            var query = Query(
                filter: q=>q.UserID == userId && q.SpaceID == spaceId && q.QaItemTypeID == Id
            );

            return await query.ToListAsync();
        }

        public async Task<Questions?> GetByIdAsync(int Id, int userId, int spaceId)
        {
            var query = Query(
               filter: q => q.ID == Id && q.UserID == userId && q.SpaceID == spaceId,
               includes:q=>q.Include(x=>x.QaItemType.QaType)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<Questions>> GetByIdsInSpaceAsync(IEnumerable<int> ids, int spaceId)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return new List<Questions>();

            return await Query(filter: q => idList.Contains(q.ID) && q.SpaceID == spaceId).ToListAsync();
        }

        public async Task<bool> AnyByQaItemTypeIdsAsync(IEnumerable<int> qaItemTypeIds)
        {
            var ids = qaItemTypeIds.ToList();
            return await Query(filter: q => ids.Contains(q.QaItemTypeID)).AnyAsync();
        }
    }
}
