using Library.Extensions;
using Microsoft.EntityFrameworkCore;
using RAG.Authentication;
using RAG.Commons.Options;
using RAG.Models.Entities;
using RAG.Repositories;
using RAG.Repositories.Interfaces;
using RAG.Services;
using RAG.Services.Interfaces;
using RAG.Skills;
using RAG.Tools;
using RAG.Spaces;


namespace RAG.Extensions
{
    public static class RegistExtension
    {
        public static IServiceCollection AddRegister (this IServiceCollection services,IHostEnvironment env,IConfigurationManager configuration)
        {
            services.AddOption(configuration)
                    .AddDb(configuration)
                    .AddLibrary(
                        "Kaikai", "Audience",
                        env,
                        swaggerOptions: s =>
                        {
                            s.Title = "RAG 應用平台";
                            s.Version = "v1";
                            s.Description = "這是一個RAG 應用平台API說明檔案";
                        }
                    )
                    .AddNativeJwtAuthentication(configuration)
                    .AddSpaceRuntime()
                    .AddSkillRuntime(configuration, env)
                    .AddToolRuntime(configuration, env)
                    .AddMapping()
                    .AddService()
                    .AddRepo();

            return services;
        }

        private static IServiceCollection AddOption(this IServiceCollection services, IConfigurationManager configuration)
        {
            var ragOption = configuration.GetSection(RagOption.SectionName).Get<RagOption>() ?? new RagOption();

            // RAG Server 內部金鑰缺少即 fail-fast；不可在 request 過程才發現或以空金鑰呼叫
            if (string.IsNullOrWhiteSpace(ragOption.InternalApiKey) || ragOption.InternalApiKey.StartsWith('<'))
            {
                throw new InvalidOperationException("RagOption:InternalApiKey is missing; set RagOption__InternalApiKey.");
            }

            services.AddSingleton(ragOption);

            return services;
        }

        private static IServiceCollection AddDb(this IServiceCollection services,IConfigurationManager configuration)
        {
            services.AddDbContext<WenDaHubContext>(options =>
            {
                options.UseMySql(
                        configuration.GetConnectionString("DefaultConnection"),
                       ServerVersion.AutoDetect(configuration.GetConnectionString("DefaultConnection"))
                    );
            });

            return services;
        }

        private static IServiceCollection AddMapping(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(Program).Assembly);

            return services;
        }

        private static IServiceCollection AddService(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IQaTypeService, QaTypeService>();
            services.AddScoped<IQaTypeItemService, QaTypeItemService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatLogRepo, ChatLogRepo>();

            return services;
        }

        private static IServiceCollection AddRepo(this IServiceCollection services)
        {
            services.AddScoped<IUserCacheRepo, UserCacheRepo>();
            services.AddScoped<IQaTypeRepo, QaTypeRepo>();
            services.AddScoped<IQaTypeItemRepo, QaTypeItemRepo>();
            services.AddScoped<IQuestionsRepo, QuestionsRepo>();
            services.AddScoped<IChatRepo, ChatRepo>();

            return services;
        }
    }
}
