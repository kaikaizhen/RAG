using Library.Http;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Authentication.ApiTokens
{
    public sealed record ApiTokenIdentity(int TokenId, int UserId, string Name, IReadOnlyList<string> Scopes, IReadOnlyList<int> SpaceIds);

    public interface IApiTokenService
    {
        Task<List<ApiToken>> ListAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>建立權杖；回傳資料列與明文（明文之後再也取不回來）</summary>
        Task<(ApiToken Token, string Plaintext)> CreateAsync(int userId, string name, IReadOnlyCollection<string> scopes, IReadOnlyCollection<int>? spaceIds, int? expiresInDays, CancellationToken cancellationToken = default);

        Task RevokeAsync(int userId, int tokenId, CancellationToken cancellationToken = default);

        /// <summary>驗證明文權杖；無效、已撤銷、已過期或帳號停用一律回傳 null</summary>
        Task<ApiTokenIdentity?> AuthenticateAsync(string plaintext, CancellationToken cancellationToken = default);
    }

    public class ApiTokenService : IApiTokenService
    {
        public const string Prefix = "rag_pat_";
        public const int MaxActiveTokensPerUser = 20;
        public const int MaxExpiryDays = 365;
        private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(10);

        private readonly WenDaHubContext _db;
        private readonly TimeProvider _time;

        public ApiTokenService(WenDaHubContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        public Task<List<ApiToken>> ListAsync(int userId, CancellationToken cancellationToken = default) =>
            _db.ApiToken.AsNoTracking()
                .Where(t => t.UserID == userId && t.RevokedAt == null)
                .OrderByDescending(t => t.CreateAt)
                .ToListAsync(cancellationToken);

        public async Task<(ApiToken Token, string Plaintext)> CreateAsync(int userId, string name, IReadOnlyCollection<string> scopes, IReadOnlyCollection<int>? spaceIds, int? expiresInDays, CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var active = await _db.ApiToken.CountAsync(t => t.UserID == userId && t.RevokedAt == null && (t.ExpiresAt == null || t.ExpiresAt > now), cancellationToken);
            if (active >= MaxActiveTokensPerUser)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"最多只能同時有 {MaxActiveTokensPerUser} 把有效的權杖，請先撤銷不用的");
            }

            var plaintext = Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));
            var token = new ApiToken
            {
                UserID = userId,
                Name = name.Trim(),
                TokenPrefix = plaintext[..12],
                TokenHash = Hash(plaintext),
                Scopes = string.Join(',', scopes.Distinct()),
                SpaceIDs = spaceIds is { Count: > 0 } ? string.Join(',', spaceIds.Distinct().Order()) : null,
                CreateAt = now,
                ExpiresAt = expiresInDays is int days ? now.AddDays(days) : null
            };

            _db.ApiToken.Add(token);
            await _db.SaveChangesAsync(cancellationToken);
            return (token, plaintext);
        }

        public async Task RevokeAsync(int userId, int tokenId, CancellationToken cancellationToken = default)
        {
            var token = await _db.ApiToken.FirstOrDefaultAsync(t => t.ID == tokenId && t.UserID == userId, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此權杖");

            if (token.RevokedAt is null)
            {
                token.RevokedAt = _time.GetUtcNow().UtcDateTime;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<ApiTokenIdentity?> AuthenticateAsync(string plaintext, CancellationToken cancellationToken = default)
        {
            if (!plaintext.StartsWith(Prefix, StringComparison.Ordinal) || plaintext.Length > 100) return null;

            var hash = Hash(plaintext);
            var token = await _db.ApiToken.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (token is null || token.RevokedAt is not null) return null;

            var now = _time.GetUtcNow().UtcDateTime;
            if (token.ExpiresAt is DateTime expires && expires <= now) return null;

            // 帳號停用後，既有權杖也立刻失效
            var userActive = await _db.UserCache.AsNoTracking().AnyAsync(u => u.ID == token.UserID && u.IsActive, cancellationToken);
            if (!userActive) return null;

            if (token.LastUsedAt is null || now - token.LastUsedAt.Value > LastUsedGranularity)
            {
                token.LastUsedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new ApiTokenIdentity(token.ID, token.UserID, token.Name, SplitScopes(token.Scopes), SplitIds(token.SpaceIDs));
        }

        public static IReadOnlyList<string> SplitScopes(string scopes) =>
            scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(ApiTokenScopes.IsValid).ToList();

        public static IReadOnlyList<int> SplitIds(string? ids) =>
            string.IsNullOrWhiteSpace(ids)
                ? Array.Empty<int>()
                : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList();

        private static string Hash(string plaintext) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
