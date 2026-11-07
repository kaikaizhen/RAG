using Microsoft.Extensions.DependencyInjection.Extensions;
using RAG.ToolRuntime;

namespace RAG.Tools
{
    public static class ToolRuntimeExtensions
    {
        /// <summary>
        /// 已註冊的固定 adapter；新增 capability 必須在此加入並撰寫對應測試
        /// </summary>
        private static readonly CapabilityDefinition[] KnownCapabilities = { KnowledgeSearchAdapter.Capability };

        public static IServiceCollection AddToolRuntime(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
        {
            var configPath = configuration["ToolRuntime:CapabilitiesFile"] is { Length: > 0 } configured ? configured : "Configuration/capabilities.json";
            var config = CapabilityConfig.Load(Path.GetFullPath(configPath, env.ContentRootPath), KnownCapabilities.Select(c => c.Id).ToList());

            services.AddSingleton(config);
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IToolAuditSink, LoggerToolAuditSink>();

            services.AddScoped<IToolAdapter, KnowledgeSearchAdapter>();
            services.AddScoped<IToolRegistry, ToolRegistry>();
            services.AddScoped<ICapabilityResolver, CapabilityResolver>();
            services.AddScoped<IToolInvoker, ToolInvoker>();

            return services;
        }
    }

    /// <summary>
    /// 以結構化 log 記錄 tool audit（不含參數值與輸出內容）
    /// </summary>
    public class LoggerToolAuditSink : IToolAuditSink
    {
        private readonly ILogger<LoggerToolAuditSink> _logger;

        public LoggerToolAuditSink(ILogger<LoggerToolAuditSink> logger) => _logger = logger;

        public void Record(ToolAuditEvent auditEvent)
        {
            _logger.LogInformation(
                "ToolAudit {CorrelationId} user={UserId} space={SpaceId} capability={CapabilityId} status={Status} durationMs={DurationMs} args={ArgumentNames}",
                auditEvent.CorrelationId, auditEvent.UserId, auditEvent.SpaceId, auditEvent.CapabilityId, auditEvent.Status, auditEvent.DurationMs, string.Join(',', auditEvent.ArgumentNames));
        }
    }
}
