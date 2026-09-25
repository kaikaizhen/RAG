namespace RAG.SpaceRuntime
{
    public enum SpaceResolutionFailure
    {
        /// <summary>
        /// 指定的 Space 不是有效 membership（含不存在、停用、未知 role）
        /// </summary>
        SelectionForbidden,

        /// <summary>
        /// 未指定 Space，且無法唯一決定（多個 membership 且無唯一預設）
        /// </summary>
        SelectionRequired,

        /// <summary>
        /// 帳號沒有任何有效 membership；不 fallback Default Space
        /// </summary>
        NoMembership
    }

    public sealed class SpaceResolution
    {
        private SpaceResolution(SpaceContext? context, SpaceResolutionFailure? failure)
        {
            Context = context;
            Failure = failure;
        }

        public SpaceContext? Context { get; }

        public SpaceResolutionFailure? Failure { get; }

        public bool Succeeded => Context is not null;

        internal static SpaceResolution Success(SpaceContext context) => new(context, null);

        internal static SpaceResolution Fail(SpaceResolutionFailure failure) => new(null, failure);
    }

    public interface IAccountSpaceResolver
    {
        /// <summary>
        /// 依已驗證的帳號與 client 提供的 Space selection hint 建立 SpaceContext
        /// </summary>
        /// <param name="userId">已驗證 principal 的帳號編號</param>
        /// <param name="spaceIdHint">client selection hint（僅作選擇，不代表授權）</param>
        Task<SpaceResolution> ResolveAsync(int userId, int? spaceIdHint, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 選擇規則：
    /// 1. 有 hint：僅在 hint 為有效 membership 時成功，否則 SelectionForbidden（不降級到其他 Space）。
    /// 2. 無 hint：唯一有效 membership → 使用；否則唯一的預設 membership → 使用；否則 SelectionRequired。
    /// 3. 沒有任何有效 membership → NoMembership（永不 fallback Default Space）。
    /// </summary>
    public class AccountSpaceResolver : IAccountSpaceResolver
    {
        private readonly ISpaceMembershipStore _store;

        public AccountSpaceResolver(ISpaceMembershipStore store)
        {
            _store = store;
        }

        public async Task<SpaceResolution> ResolveAsync(int userId, int? spaceIdHint, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return SpaceResolution.Fail(SpaceResolutionFailure.NoMembership);

            var memberships = (await _store.GetActiveMembershipsAsync(userId, cancellationToken))
                .Where(m => SpaceRoles.TryGetPermissions(m.Role, out _))
                .ToList();

            if (spaceIdHint is int hint)
            {
                var selected = memberships.Where(m => m.SpaceId == hint).ToList();
                return selected.Count == 1
                    ? Success(userId, selected[0])
                    : SpaceResolution.Fail(SpaceResolutionFailure.SelectionForbidden);
            }

            if (memberships.Count == 0) return SpaceResolution.Fail(SpaceResolutionFailure.NoMembership);

            if (memberships.Count == 1) return Success(userId, memberships[0]);

            var defaults = memberships.Where(m => m.IsDefault).ToList();
            return defaults.Count == 1
                ? Success(userId, defaults[0])
                : SpaceResolution.Fail(SpaceResolutionFailure.SelectionRequired);
        }

        private static SpaceResolution Success(int userId, SpaceMembership membership)
        {
            SpaceRoles.TryGetPermissions(membership.Role, out var permissions);
            return SpaceResolution.Success(new SpaceContext(userId, membership, permissions));
        }
    }
}
