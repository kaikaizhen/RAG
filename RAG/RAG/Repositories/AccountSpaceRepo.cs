using Exercise.Repositories;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.SpaceRuntime;

namespace RAG.Repositories
{
    /// <summary>
    /// Space membership 的資料來源（AccountSpace + Space）
    /// </summary>
    public class AccountSpaceRepo : BaseRepo<WenDaHubContext, AccountSpace>, ISpaceMembershipStore
    {
        public AccountSpaceRepo(WenDaHubContext context) : base(context) { }

        public async Task<IReadOnlyList<SpaceMembership>> GetActiveMembershipsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var query = QueryAsync(
                filter: q => q.UserID == userId && q.IsActive && q.Space.IsActive,
                orderBy: q => q.OrderBy(x => x.SpaceID)
            );

            return await query
                .Select(x => new SpaceMembership(x.SpaceID, x.Space.Key, x.Space.Name, x.Role, x.IsDefault))
                .ToListAsync(cancellationToken);
        }
    }
}
