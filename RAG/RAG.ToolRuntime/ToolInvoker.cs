using RAG.SpaceRuntime;
using System.Diagnostics;
using System.Text.Json;

namespace RAG.ToolRuntime
{
    public enum ToolInvocationStatus
    {
        Succeeded,

        /// <summary>
        /// 未知、未在此 Space 開放、或角色無權限（不區分，避免洩漏 capability 是否存在）
        /// </summary>
        Forbidden,

        InvalidArguments,
        TimedOut,
        Cancelled,
        Failed
    }

    public sealed record ToolInvocationResult(ToolInvocationStatus Status, object? Output = null, string? Error = null);

    /// <summary>
    /// 稽核事件：不含參數值與輸出內容，避免記錄 credential、prompt 或敏感原文
    /// </summary>
    public sealed record ToolAuditEvent(
        DateTimeOffset Timestamp,
        string CorrelationId,
        int UserId,
        int SpaceId,
        string CapabilityId,
        ToolInvocationStatus Status,
        long DurationMs,
        IReadOnlyList<string> ArgumentNames);

    public interface IToolAuditSink
    {
        void Record(ToolAuditEvent auditEvent);
    }

    public interface IToolInvoker
    {
        /// <summary>
        /// 唯一執行入口：每次都重新授權（Space 允許清單 + 角色權限），驗證參數、套用 timeout 並寫入 audit
        /// </summary>
        Task<ToolInvocationResult> InvokeAsync(SpaceExecutionContext context, string capabilityId, IReadOnlyDictionary<string, JsonElement> arguments, string correlationId, CancellationToken cancellationToken = default);
    }

    public class ToolInvoker : IToolInvoker
    {
        private const string InvalidCapabilityMarker = "<invalid>";

        private readonly ICapabilityResolver _resolver;
        private readonly IToolAuditSink _audit;
        private readonly TimeProvider _timeProvider;

        public ToolInvoker(ICapabilityResolver resolver, IToolAuditSink audit, TimeProvider timeProvider)
        {
            _resolver = resolver;
            _audit = audit;
            _timeProvider = timeProvider;
        }

        public async Task<ToolInvocationResult> InvokeAsync(SpaceExecutionContext context, string capabilityId, IReadOnlyDictionary<string, JsonElement> arguments, string correlationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            var started = Stopwatch.StartNew();
            var auditedId = CapabilityIds.IsValid(capabilityId) ? capabilityId : InvalidCapabilityMarker;
            var argumentNames = arguments.Keys.Where(CapabilityArgumentNames.IsSafe).OrderBy(n => n, StringComparer.Ordinal).ToList();

            ToolInvocationResult Audit(ToolInvocationResult result)
            {
                _audit.Record(new ToolAuditEvent(_timeProvider.GetUtcNow(), correlationId, context.Space.UserId, context.Space.SpaceId, auditedId, result.Status, started.ElapsedMilliseconds, argumentNames));
                return result;
            }

            if (!_resolver.TryResolve(context, capabilityId, out var adapter))
            {
                return Audit(new ToolInvocationResult(ToolInvocationStatus.Forbidden, Error: "Forbidden"));
            }

            if (!TryValidate(adapter.Definition, arguments, out var validated, out var argumentError))
            {
                return Audit(new ToolInvocationResult(ToolInvocationStatus.InvalidArguments, Error: argumentError));
            }

            using var timeout = new CancellationTokenSource(adapter.Definition.Timeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                var output = await adapter.InvokeAsync(new ToolInvocation(context, validated, correlationId), linked.Token);
                return Audit(new ToolInvocationResult(ToolInvocationStatus.Succeeded, output));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Audit(new ToolInvocationResult(ToolInvocationStatus.Cancelled, Error: "Cancelled"));
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return Audit(new ToolInvocationResult(ToolInvocationStatus.TimedOut, Error: "Timed out"));
            }
            catch (Exception)
            {
                // 不回傳例外訊息，避免洩漏內部細節或 credential
                return Audit(new ToolInvocationResult(ToolInvocationStatus.Failed, Error: "Tool execution failed"));
            }
        }

        private static bool TryValidate(CapabilityDefinition definition, IReadOnlyDictionary<string, JsonElement> arguments, out IReadOnlyDictionary<string, object> validated, out string error)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            validated = result;
            error = string.Empty;

            var declared = definition.Arguments.ToDictionary(a => a.Name, StringComparer.Ordinal);
            var unknown = arguments.Keys.FirstOrDefault(k => !declared.ContainsKey(k));
            if (unknown is not null) { error = "Unknown argument"; return false; }

            foreach (var spec in definition.Arguments)
            {
                if (!arguments.TryGetValue(spec.Name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    if (spec.Required) { error = $"Argument '{spec.Name}' is required"; return false; }
                    continue;
                }

                switch (spec.Type)
                {
                    case ToolArgumentType.String when value.ValueKind == JsonValueKind.String:
                        var text = value.GetString()!;
                        if (text.Length == 0 || text.Length > spec.MaxLength) { error = $"Argument '{spec.Name}' length is invalid"; return false; }
                        result[spec.Name] = text;
                        break;
                    case ToolArgumentType.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number):
                        result[spec.Name] = number;
                        break;
                    case ToolArgumentType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                        result[spec.Name] = value.GetBoolean();
                        break;
                    default:
                        error = $"Argument '{spec.Name}' must be {spec.Type}";
                        return false;
                }
            }

            return true;
        }
    }

    internal static class CapabilityArgumentNames
    {
        public static bool IsSafe(string name) => name.Length is > 0 and <= 64 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }
}
