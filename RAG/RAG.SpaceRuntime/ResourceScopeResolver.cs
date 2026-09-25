namespace RAG.SpaceRuntime
{
    public sealed class ResourceScopeResolution
    {
        private ResourceScopeResolution(ResourceScope? scope) => Scope = scope;

        public ResourceScope? Scope { get; }

        public bool Succeeded => Scope is not null;

        internal static ResourceScopeResolution Success(ResourceScope scope) => new(scope);

        internal static readonly ResourceScopeResolution Rejected = new(null);
    }

    public interface IResourceScopeResolver
    {
        /// <summary>
        /// 驗證 client 的 repository / project selection hint 並建立 ResourceScope
        /// </summary>
        ResourceScopeResolution Resolve(SpaceContext space, string? repositoryIdHint, string? projectIdHint);
    }

    /// <summary>
    /// 目前沒有 Space-scoped 的 repository / project 權威來源：
    /// 未指定 → 空 ResourceScope（不放寬任何查詢）；指定任何值 → 拒絕（fail-closed），不可以 client 值擴大範圍。
    /// </summary>
    public class ResourceScopeResolver : IResourceScopeResolver
    {
        public ResourceScopeResolution Resolve(SpaceContext space, string? repositoryIdHint, string? projectIdHint)
        {
            ArgumentNullException.ThrowIfNull(space);

            if (string.IsNullOrEmpty(repositoryIdHint) && string.IsNullOrEmpty(projectIdHint))
            {
                return ResourceScopeResolution.Success(ResourceScope.Empty);
            }

            return ResourceScopeResolution.Rejected;
        }
    }
}
