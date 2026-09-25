namespace RAG.SpaceRuntime
{
    public static class SpacePermissions
    {
        public const string SpaceRead = "space.read";
        public const string SpaceManage = "space.manage";
        public const string KnowledgeRead = "knowledge.read";
        public const string KnowledgeWrite = "knowledge.write";
    }

    /// <summary>
    /// Space role → permissions。未知 role 視為無效 membership（fail-closed）。
    /// </summary>
    public static class SpaceRoles
    {
        public const string Owner = "owner";
        public const string Member = "member";
        public const string Viewer = "viewer";

        private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [Owner] = new HashSet<string> { SpacePermissions.SpaceRead, SpacePermissions.SpaceManage, SpacePermissions.KnowledgeRead, SpacePermissions.KnowledgeWrite },
                [Member] = new HashSet<string> { SpacePermissions.SpaceRead, SpacePermissions.KnowledgeRead, SpacePermissions.KnowledgeWrite },
                [Viewer] = new HashSet<string> { SpacePermissions.SpaceRead, SpacePermissions.KnowledgeRead },
            };

        public static bool TryGetPermissions(string role, out IReadOnlySet<string> permissions)
        {
            if (RolePermissions.TryGetValue(role, out var found))
            {
                permissions = found;
                return true;
            }

            permissions = new HashSet<string>();
            return false;
        }
    }
}
