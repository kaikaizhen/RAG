namespace RAG.Context
{
    public static class ContextRuntimeExtensions
    {
        public static IServiceCollection AddContextRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection(ContextRuntimeOptions.SectionName).Get<ContextRuntimeOptions>() ?? new ContextRuntimeOptions();
            options.Validate();

            services.AddSingleton(options);
            services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();

            return services;
        }
    }
}
