namespace RAG.SpaceRuntime
{
    /// <summary>
    /// 伺服器端唯一有效的 Space authorization 結果；僅能由 <see cref="IAccountSpaceResolver"/> 依已驗證 principal 與 membership 建立。
    /// </summary>
    public sealed class SpaceContext
    {
        internal SpaceContext(int userId, SpaceMembership membership, IReadOnlySet<string> permissions)
        {
            UserId = userId;
            SpaceId = membership.SpaceId;
            SpaceKey = membership.SpaceKey;
            SpaceName = membership.SpaceName;
            Role = membership.Role;
            Permissions = permissions;
        }

        /// <summary>
        /// 帳號（UserCache.ID）
        /// </summary>
        public int UserId { get; }

        public int SpaceId { get; }

        public string SpaceKey { get; }

        public string SpaceName { get; }

        public string Role { get; }

        public IReadOnlySet<string> Permissions { get; }

        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }
}
