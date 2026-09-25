using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class ChatRepo:BaseRepo<WenDaHubContext, ChatHeader>,IChatRepo
    {
        public ChatRepo(WenDaHubContext context) : base(context) { }

        public async Task<ChatHeader?> GetByIdAsync(int Id, int userId, int spaceId)
        {
            var query = QueryAsync(
               filter: q => q.ID == Id && q.UserID == userId && q.SpaceID == spaceId
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<ChatHeader>> GetByUserIdAsync(int Id, int spaceId)
        {
            var query = QueryAsync(
                filter:q=>q.UserID == Id && q.SpaceID == spaceId,
                orderBy:q=>q.OrderByDescending(q=>q.UpdateAt)
                                .ThenByDescending(x => x.CreateAt)
            );

            return await query.ToListAsync();
        }

        public async Task AddAsync(ChatHeader entity)
        {
            await base.AddAsync(entity);
        }

        public async Task DeleteAsync(ChatHeader entity)
        {
            await base.DeleteAsync(entity);
        }
    }
}
