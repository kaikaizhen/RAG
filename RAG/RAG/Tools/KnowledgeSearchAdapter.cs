using RAG.Context;
using RAG.SpaceRuntime;
using RAG.ToolRuntime;

namespace RAG.Tools
{
    /// <summary>
    /// knowledge.search：只在目前 ExecutionContext 的 Space 內搜尋知識（唯讀）
    /// </summary>
    public class KnowledgeSearchAdapter : IToolAdapter
    {
        public static readonly CapabilityDefinition Capability = new(
            Id: "knowledge.search",
            Description: "在目前 Space 的知識庫中檢索證據（只檢索、不生成答案）",
            Kind: CapabilityKind.Read,
            RequiredPermission: SpacePermissions.KnowledgeRead,
            Timeout: TimeSpan.FromSeconds(30),
            Arguments: new[]
            {
                new ToolArgument("query", ToolArgumentType.String, Required: true, MaxLength: 500),
                new ToolArgument("type", ToolArgumentType.String, Required: false, MaxLength: 20),
            });

        private readonly IKnowledgeSearchService _knowledgeSearch;

        public KnowledgeSearchAdapter(IKnowledgeSearchService knowledgeSearch)
        {
            _knowledgeSearch = knowledgeSearch;
        }

        public CapabilityDefinition Definition => Capability;

        public async Task<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
        {
            var query = (string)invocation.Arguments["query"];
            invocation.Arguments.TryGetValue("type", out var type);

            // 與 /api/v1/Knowledge/search 相同的證據規則（MySQL 回查、Superseded 排除、門檻分類）
            return await _knowledgeSearch.SearchAsync(invocation.Context, query, type as string, cancellationToken);
        }
    }
}
