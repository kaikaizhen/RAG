using Library.Http;
using RAG.Authentication.ApiTokens;
using RAG.SpaceRuntime;
using System.Globalization;
using System.Net;

namespace RAG.Context
{
    public static class SearchScopes
    {
        /// <summary>只找目前的 Space（預設）</summary>
        public const string Current = "current";

        /// <summary>找我所有可以讀的 Space</summary>
        public const string All = "all";

        public static bool IsValid(string? scope) => scope is null or Current or All;
    }

    /// <summary>
    /// 一次檢索實際涵蓋的 Space（已由伺服器依 membership 與權限決定）。Primary 是目前的 Space
    /// </summary>
    public sealed record ResolvedSearchScope(string Scope, IReadOnlyList<SpaceMembership> Spaces)
    {
        public IReadOnlyList<int> SpaceIds => Spaces.Select(s => s.SpaceId).ToList();
    }

    public interface ISearchScopeResolver
    {
        /// <summary>
        /// 把 client 的「current / all」提示換成實際的 Space 清單。client 只能選「範圍的種類」，不能指定任何 Space 編號
        /// </summary>
        Task<ResolvedSearchScope> ResolveAsync(SpaceExecutionContext context, string? scope, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// all = 帳號所有「啟用中、角色有 knowledge.read」的 Space；若是限定 Space 的應用程式權杖，再與它允許的 Space 取交集。
    /// 目前的 Space 一律包含在內（它已通過 SpaceContext 授權）
    /// </summary>
    public sealed class SearchScopeResolver : ISearchScopeResolver
    {
        private readonly ISpaceMembershipStore _store;
        private readonly IHttpContextAccessor _http;

        public SearchScopeResolver(ISpaceMembershipStore store, IHttpContextAccessor http)
        {
            _store = store;
            _http = http;
        }

        public async Task<ResolvedSearchScope> ResolveAsync(SpaceExecutionContext context, string? scope, CancellationToken cancellationToken = default)
        {
            if (!SearchScopes.IsValid(scope)) throw new HttpException(HttpStatusCode.BadRequest, "scope 只能是 current 或 all");

            var space = context.Space;
            var current = new SpaceMembership(space.SpaceId, space.SpaceKey, space.SpaceName, space.Role, IsDefault: false);
            if (scope != SearchScopes.All) return new ResolvedSearchScope(SearchScopes.Current, new[] { current });

            var user = _http.HttpContext?.User;
            var tokenSpaces = user?.HasClaim(ApiTokenClaims.TokenUse, ApiTokenClaims.PersonalAccessToken) == true
                ? user.FindAll(ApiTokenClaims.AllowedSpace).Select(c => int.Parse(c.Value, CultureInfo.InvariantCulture)).ToHashSet()
                : new HashSet<int>();

            var readable = (await _store.GetActiveMembershipsAsync(space.UserId, cancellationToken))
                .Where(m => SpaceRoles.TryGetPermissions(m.Role, out var permissions) && permissions.Contains(SpacePermissions.KnowledgeRead))
                .Where(m => tokenSpaces.Count == 0 || tokenSpaces.Contains(m.SpaceId))
                .Where(m => m.SpaceId != space.SpaceId)
                .Prepend(current)
                .OrderBy(m => m.SpaceId == space.SpaceId ? 0 : 1).ThenBy(m => m.SpaceId)
                .ToList();

            return new ResolvedSearchScope(SearchScopes.All, readable);
        }
    }
}
