using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class ChatLogRepo:BaseRepo<WenDaHubContext,ChatLog>,IChatLogRepo
    {
        public ChatLogRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<ChatLog>> GetAsync(int Id)
        {
            var query = QueryAsync(
                filter:q=>q.ChatHeaderID == Id
            );

            return await query.ToListAsync();
        }

        public async Task DeleteRangeAsync(List<ChatLog> entities)
        {
            await base.DeleteRangeAsync(entities);
        }
    }
}
