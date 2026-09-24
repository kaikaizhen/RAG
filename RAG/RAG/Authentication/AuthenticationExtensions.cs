using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TD_WebApiHelper.Core.Helpers;

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
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

            // Library 的 JwtTokenHelper 不再是 authentication authority，也不得在 request 中自動產生 secret
            services.Replace(ServiceDescriptor.Singleton<IJwtTokenHelper, DisabledLibraryJwtTokenHelper>());

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(jwtOptions.SigningKey),
                        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                        ClockSkew = TimeSpan.Zero
                    };
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
                options.FallbackPolicy = authenticatedUser;
            });

            services.ConfigureSwaggerGen(options =>
            {
                var scheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "輸入登入取得的 AccessToken",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
                };

                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
            });

            return services;
        }
    }
}
