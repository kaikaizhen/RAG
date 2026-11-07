using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RAG.Authentication
{
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// 原生 ASP.NET Core JWT Bearer authentication / authorization：
        /// 預設所有 endpoint 需要已驗證使用者，僅明確標示 [AllowAnonymous] 者例外。
        /// </summary>
        public static IServiceCollection AddNativeJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = JwtOptions.Load(configuration);

            services.AddSingleton(jwtOptions);
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
            services.AddSingleton<IRefreshTokenValidator, RefreshTokenValidator>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = true;
                    options.TokenValidationParameters = jwtOptions.CreateValidationParameters();
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var tokenUse = context.Principal?.FindFirst(JwtOptions.TokenUseClaim)?.Value;

                            if (tokenUse != JwtOptions.AccessTokenUse)
                            {
                                context.Fail("Token is not an access token");
                            }
                            else if (!CurrentUserAccessor.TryGetUserId(context.Principal, out _))
                            {
                                context.Fail("Token subject is invalid");
                            }

                            return Task.CompletedTask;
                        },
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                        },
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new { error = "Forbidden" });
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                var authenticatedUser = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();

                options.DefaultPolicy = authenticatedUser;

                // 預設所有 endpoint 需要登入；僅 Library 掛載的 OpenAPI 文件（/openapi/*，與舊的 Swagger JSON 一樣公開）例外
                options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAssertion(context =>
                        context.User.Identity?.IsAuthenticated == true
                        || (context.Resource is HttpContext http && http.Request.Path.StartsWithSegments("/openapi")))
                    .Build();
            });

            // Library 的 OpenAPI 只負責文件與版本；Bearer 驗證方式由應用程式自己宣告
            services.ConfigureAll<OpenApiOptions>(options =>
            {
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                    {
                        Description = "輸入登入取得的 AccessToken",
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    };
                    document.Security ??= new List<OpenApiSecurityRequirement>();
                    document.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = new List<string>()
                    });

                    return Task.CompletedTask;
                });
            });

            return services;
        }
    }
}
