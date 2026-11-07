namespace RAG.Spaces
{
    /// <summary>
    /// 標記需要已解析 SpaceContext 的 endpoint；由 <see cref="SpaceContextMiddleware"/> 在 authorization 之後解析
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireSpaceContextAttribute : Attribute
    {
    }

    public static class SpaceSelectionHeaders
    {
        /// <summary>
        /// Space selection hint（唯一接受的來源；body / query 的 space_id 一律忽略）
        /// </summary>
        public const string SpaceId = "X-Space-Id";

        public const string RepositoryId = "X-Repository-Id";

        public const string ProjectId = "X-Project-Id";
    }
}
