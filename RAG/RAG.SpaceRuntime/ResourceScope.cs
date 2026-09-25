namespace RAG.SpaceRuntime
{
    /// <summary>
    /// Request-level 資源範圍（repository / project）。不屬於 identity；僅能由 <see cref="IResourceScopeResolver"/> 驗證後建立。
    /// </summary>
    public sealed class ResourceScope
    {
        public static readonly ResourceScope Empty = new(null, null);

        private ResourceScope(string? repositoryId, string? projectId)
        {
            RepositoryId = repositoryId;
            ProjectId = projectId;
        }

        public string? RepositoryId { get; }

        public string? ProjectId { get; }

        public bool IsEmpty => RepositoryId is null && ProjectId is null;
    }
}
