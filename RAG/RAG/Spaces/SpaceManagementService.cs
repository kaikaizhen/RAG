using Library.Http;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.SpaceRuntime;
using System.Net;
using System.Threading.RateLimiting;

namespace RAG.Spaces
{
    public interface ISpaceManagementService
    {
        /// <summary>建立一個新空間，建立者是它的 owner（不會改變帳號的預設空間）</summary>
        Task<(Space Space, AccountSpace Membership)> CreateAsync(int userId, string name, CancellationToken cancellationToken = default);

        /// <summary>重新命名；只有該空間的 owner 可以</summary>
        Task<(Space Space, AccountSpace Membership)> RenameAsync(int userId, int spaceId, string name, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 使用者自己建立空間：知識庫、對話、暫存區都以空間隔離，建立者是 owner，之後才能再分享給別人。
    /// 空間的建立與命名只動 Space / AccountSpace 兩張表，不會碰到任何既有空間的資料
    /// </summary>
    public sealed class SpaceManagementService : ISpaceManagementService
    {
        public const int MaxOwnedSpaces = 10;

        private readonly WenDaHubContext _db;
        private readonly TimeProvider _time;

        public SpaceManagementService(WenDaHubContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        public async Task<(Space Space, AccountSpace Membership)> CreateAsync(int userId, string name, CancellationToken cancellationToken = default)
        {
            name = ValidateName(name);

            var owned = await _db.AccountSpace.CountAsync(m => m.UserID == userId && m.Role == SpaceRoles.Owner && m.IsActive && m.Space.IsActive, cancellationToken);
            if (owned >= MaxOwnedSpaces)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"最多只能擁有 {MaxOwnedSpaces} 個空間");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var membership = new AccountSpace { UserID = userId, Role = SpaceRoles.Owner, IsActive = true, IsDefault = false, CreateAt = now };
            var space = new Space
            {
                // t- 開頭的隨機識別字（個人空間是 u-<編號>）；唯一索引保證不會重複
                Key = $"t-{Guid.NewGuid():N}"[..14],
                Name = name,
                IsActive = true,
                CreateAt = now
            };
            space.AccountSpace.Add(membership);

            _db.Space.Add(space);
            await _db.SaveChangesAsync(cancellationToken);
            return (space, membership);
        }

        public async Task<(Space Space, AccountSpace Membership)> RenameAsync(int userId, int spaceId, string name, CancellationToken cancellationToken = default)
        {
            name = ValidateName(name);

            var membership = await _db.AccountSpace.Include(m => m.Space)
                .FirstOrDefaultAsync(m => m.UserID == userId && m.SpaceID == spaceId && m.IsActive && m.Space.IsActive, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此空間");
            if (membership.Role != SpaceRoles.Owner) throw new HttpException(HttpStatusCode.Forbidden, "只有空間的擁有者可以重新命名");

            membership.Space.Name = name;
            await _db.SaveChangesAsync(cancellationToken);
            return (membership.Space, membership);
        }

        private static string ValidateName(string name)
        {
            name = name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > 100) throw new HttpException(HttpStatusCode.BadRequest, "空間名稱必須是 1–100 個字");
            return name;
        }
    }

    public static class SpaceManagementRateLimits
    {
        public const string Create = "space-create";
    }

    public static class SpaceManagementExtensions
    {
        public static IServiceCollection AddSpaceManagement(this IServiceCollection services, IConfiguration configuration)
        {
            var perMinute = configuration.GetValue("SpaceManagement:CreatePerMinute", 5);
            services.AddScoped<ISpaceManagementService, SpaceManagementService>();

            // 建立空間：每個使用者每分鐘 5 次（可由 SpaceManagement:CreatePerMinute 調整；另有每人 10 個空間的上限）
            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(SpaceManagementRateLimits.Create, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            return services;
        }
    }
}
