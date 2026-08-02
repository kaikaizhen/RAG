using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;

namespace RAG.Repositories
{
    public class UserCacheRepo: BaseRepo<WenDaHubContext,UserCache>,IUserCacheRepo
    {
        public UserCacheRepo(WenDaHubContext context) : base(context) { }

        public async Task<UserCache?> GetUserByAccountAndPasswordAsync(string account,string password)
        {
            var query = QueryAsync(
                filter: s => s.Account == account && s.Password == password,
                asNoTracking:false
                );

            return await query.FirstOrDefaultAsync();
        }

        public async Task<bool> IncludeAccountAsync(string account)
        {
            var query = QueryAsync(
                filter:s=>s.Account ==account
            );

            return await query.AnyAsync();
        }

        public async Task<UserCache?> GetByIdAsync(int Id)
        {
            var query = QueryAsync(
                filter:s=>s.ID == Id,
                includes:u=>u.Include(x=>x.Questions)
            );

            return await query.FirstOrDefaultAsync();
        }

        public async Task AddAsync(UserCache entity)
        {
            await base.AddAsync(entity);
        }

        public async Task UpdateAsync(UserCache entity)
        {
            await base.UpdateAsync(entity);
        }

        public async Task DeleteAsync(UserCache entity)
        {
            await base.DeleteAsync(entity);
        }
    }
}
