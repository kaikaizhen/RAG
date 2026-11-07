using RAG.SkillRuntime;

namespace RAG.Skills
{
    public static class SkillRuntimeExtensions
    {
        /// <summary>
        /// SkillRuntime:Root 未設定 → 停用（resolver 回傳 null，API 回 503）；
        /// 已設定 → 啟動時載入並驗證全部 manifest 與 spaces.json，任何錯誤即 fail-fast。
        /// </summary>
        public static IServiceCollection AddSkillRuntime(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
        {
            var options = configuration.GetSection(SkillRuntimeOptions.SectionName).Get<SkillRuntimeOptions>() ?? new SkillRuntimeOptions();

            var registry = string.IsNullOrWhiteSpace(options.Root)
                ? SkillRegistry.Disabled
                : SkillRegistry.Load(Path.GetFullPath(options.Root, env.ContentRootPath), options.MaxFileBytes);

            services.AddSingleton<ISkillRegistry>(registry);
            services.AddSingleton<ITokenEstimator, ApproximateTokenEstimator>();
            services.AddSingleton<ISkillResolver, SkillResolver>();
            services.AddSingleton<ISkillBundleLoader, SkillBundleLoader>();

            return services;
        }
    }
}
