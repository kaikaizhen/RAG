using RAG.Rag;
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
            Description: "在目前 Space 的知識庫中搜尋並回傳答案與相關問題",
            Kind: CapabilityKind.Read,
            RequiredPermission: SpacePermissions.KnowledgeRead,
            Timeout: TimeSpan.FromSeconds(30),
            Arguments: new[]
            {
                new ToolArgument("query", ToolArgumentType.String, Required: true, MaxLength: 500),
                new ToolArgument("type", ToolArgumentType.String, Required: false, MaxLength: 20),
            });

        private readonly IRagGateway _ragGateway;
        private readonly IRagScopeFactory _ragScopeFactory;

        public KnowledgeSearchAdapter(IRagGateway ragGateway, IRagScopeFactory ragScopeFactory)
        {
            _ragGateway = ragGateway;
            _ragScopeFactory = ragScopeFactory;
        }

        public CapabilityDefinition Definition => Capability;

        public async Task<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
        {
            var query = (string)invocation.Arguments["query"];
            invocation.Arguments.TryGetValue("type", out var type);

            var result = await _ragGateway.AskAsync(_ragScopeFactory.Create(invocation.Context), query, type as string, cancellationToken);

            return new { answer = result.Answer, confidence = result.Confidence, related = result.Related };
        }
    }
}
