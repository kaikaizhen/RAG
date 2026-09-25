using RAG.Repositories;
using RAG.SpaceRuntime;

namespace RAG.Spaces
{
    public static class SpaceRuntimeExtensions
    {
        public static IServiceCollection AddSpaceRuntime(this IServiceCollection services)
        {
            services.AddScoped<ISpaceMembershipStore, AccountSpaceRepo>();
            services.AddScoped<IAccountSpaceResolver, AccountSpaceResolver>();
            services.AddSingleton<IResourceScopeResolver, ResourceScopeResolver>();
            services.AddScoped<ISpaceContextAccessor, SpaceContextAccessor>();

            return services;
        }

        /// <summary>
        /// 必須位於 UseAuthentication / UseAuthorization 之後
        /// </summary>
        public static IApplicationBuilder UseSpaceContext(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SpaceContextMiddleware>();
        }
    }
}
