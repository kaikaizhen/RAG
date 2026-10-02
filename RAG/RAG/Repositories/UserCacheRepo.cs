using Library.Repo;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class UserCacheRepo: RagRepo<UserCache>,IUserCacheRepo
    {
        public UserCacheRepo(WenDaHubContext context) : base(context) { }

        public async Task<UserCache?> GetUserByAccountAndPasswordAsync(string account,string password)
        {
            var query = Query(
                filter: s => s.Account == account && s.Password == password,
                asNoTracking:false
                );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<bool> IncludeAccountAsync(string account)
        {
            var query = Query(
                filter:s=>s.Account ==account
            );

            return await query.AnyAsync();
        }

        public async Task<UserCache?> GetByIdAsync(int Id)
        {
            var query = Query(
                filter:s=>s.ID == Id,
                includes:u=>u.Include(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }
    }
}
