using RAG.SpaceRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.ToolRuntime
{
    public sealed record ToolInvocation(SpaceExecutionContext Context, IReadOnlyDictionary<string, object> Arguments, string CorrelationId);

    public interface IToolAdapter
    {
        CapabilityDefinition Definition { get; }

        /// <summary>
        /// 僅由 <see cref="IToolInvoker"/> 在授權與參數驗證後呼叫
        /// </summary>
        Task<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken);
    }

    public interface IToolRegistry
    {
        bool TryGet(string capabilityId, out IToolAdapter adapter);

        IReadOnlyCollection<string> Ids { get; }
    }

    public sealed class ToolRegistry : IToolRegistry
    {
        private readonly IReadOnlyDictionary<string, IToolAdapter> _adapters;

        public ToolRegistry(IEnumerable<IToolAdapter> adapters)
        {
            var map = new Dictionary<string, IToolAdapter>(StringComparer.Ordinal);
            foreach (var adapter in adapters)
            {
                var id = adapter.Definition.Id;
                if (!CapabilityIds.IsValid(id)) throw new InvalidOperationException($"Invalid capability id '{id}'");
                if (!map.TryAdd(id, adapter)) throw new InvalidOperationException($"Duplicate capability '{id}'");
            }
            _adapters = map;
        }

        public IReadOnlyCollection<string> Ids => _adapters.Keys.ToList();

        public bool TryGet(string capabilityId, out IToolAdapter adapter) => _adapters.TryGetValue(capabilityId, out adapter!);
    }

    /// <summary>
    /// capabilities.json：每個 Space（以 Space.Key）明確列出可用 capability ID；不從 Default 繼承
    /// </summary>
    public sealed class CapabilityConfig
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
        [JsonPropertyName("spaces")] public Dictionary<string, List<string>> Spaces { get; init; } = new();

        public static CapabilityConfig Load(string path, IReadOnlyCollection<string> knownCapabilityIds)
        {
            if (!File.Exists(path)) throw new InvalidOperationException($"Capability config not found: {path}");

            CapabilityConfig config;
            try
            {
                config = JsonSerializer.Deserialize<CapabilityConfig>(File.ReadAllText(path)) ?? throw new JsonException("empty");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Capability config is invalid JSON: {path}", ex);
            }

            var errors = new List<string>();
            if (config.SchemaVersion != 1) errors.Add($"unsupported schemaVersion {config.SchemaVersion}");
            foreach (var (spaceKey, ids) in config.Spaces)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(spaceKey, "^[a-z0-9][a-z0-9-]{0,63}$")) errors.Add($"'{spaceKey}' is not a valid space key");
                foreach (var id in ids)
                {
                    if (!CapabilityIds.IsValid(id)) errors.Add($"space '{spaceKey}': '{id}' is not a valid capability id");
                    else if (!knownCapabilityIds.Contains(id)) errors.Add($"space '{spaceKey}': unknown capability '{id}'");
                }
            }

            if (errors.Count > 0) throw new InvalidOperationException("Invalid capability config:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
            return config;
        }
    }

    public interface ICapabilityResolver
    {
        /// <summary>
        /// 目前 ExecutionContext 可見（亦即可執行）的 capability：Space 允許清單 ∩ 已註冊 adapter ∩ 角色權限
        /// </summary>
        IReadOnlyList<CapabilityDefinition> Resolve(SpaceExecutionContext context);

        bool TryResolve(SpaceExecutionContext context, string capabilityId, out IToolAdapter adapter);
    }

    public class CapabilityResolver : ICapabilityResolver
    {
        private readonly IToolRegistry _registry;
        private readonly CapabilityConfig _config;

        public CapabilityResolver(IToolRegistry registry, CapabilityConfig config)
        {
            _registry = registry;
            _config = config;
        }

        public IReadOnlyList<CapabilityDefinition> Resolve(SpaceExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!_config.Spaces.TryGetValue(context.Space.SpaceKey, out var allowed)) return Array.Empty<CapabilityDefinition>();

            return allowed
                .Distinct(StringComparer.Ordinal)
                .Select(id => TryResolve(context, id, out var adapter) ? adapter.Definition : null)
                .OfType<CapabilityDefinition>()
                .ToList();
        }

        public bool TryResolve(SpaceExecutionContext context, string capabilityId, out IToolAdapter adapter)
        {
            ArgumentNullException.ThrowIfNull(context);
            adapter = null!;

            return CapabilityIds.IsValid(capabilityId)
                && _config.Spaces.TryGetValue(context.Space.SpaceKey, out var allowed)
                && allowed.Contains(capabilityId, StringComparer.Ordinal)
                && _registry.TryGet(capabilityId, out adapter)
                && context.Space.HasPermission(adapter.Definition.RequiredPermission);
        }
    }
}
