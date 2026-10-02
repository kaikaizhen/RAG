using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class ChatLogRepo:RagRepo<ChatLog>,IChatLogRepo
    {
        public ChatLogRepo(WenDaHubContext context) : base(context) { }

        public async Task<List<ChatLog>> GetAsync(int Id, int userId, int spaceId)
        {
            var query = Query(
                filter:q=>q.ChatHeaderID == Id && q.ChatHeader.UserID == userId && q.ChatHeader.SpaceID == spaceId
            );

            return await query.ToListAsync();
        }
    }
}
