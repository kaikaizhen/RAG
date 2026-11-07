using System.Threading.RateLimiting;
using Library.Extensions;
using Library.Mail;
using Microsoft.AspNetCore.RateLimiting;

namespace RAG.Registration
{
    public static class RegistrationRateLimits
    {
        public const string Register = "registration-register";
        public const string Verify = "registration-verify";
    }

    public static class RegistrationExtensions
    {
        /// <summary>
        /// 註冊 / 驗證信箱 / 寄信。自行註冊預設關閉；啟用時缺少寄信或連結設定即啟動失敗（不在 request 時才發現）。
        /// </summary>
        public static IServiceCollection AddRegistration(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection(RegistrationOptions.SectionName).Get<RegistrationOptions>() ?? new RegistrationOptions();
            var mailConfigured = !string.IsNullOrWhiteSpace(configuration[$"{MailOptions.SectionName}:Host"]);

            if (options.Enabled)
            {
                if (!mailConfigured)
                    throw new InvalidOperationException("Registration:Enabled requires mail settings; set Library__Mail__Host / Account / Password.");
                if (string.IsNullOrWhiteSpace(options.VerifyUrlBase) || !Uri.TryCreate(options.VerifyUrlBase, UriKind.Absolute, out _))
                    throw new InvalidOperationException("Registration:Enabled requires an absolute Registration:VerifyUrlBase.");
            }

            // 寄信設定存在才註冊（AddLibraryMail 會在啟動時驗證必填欄位）
            if (mailConfigured) services.AddLibraryMail(configuration);

            services.AddMemoryCache();
            services.AddSingleton(options);
            services.AddSingleton<Authentication.IPasswordHasher, Authentication.Pbkdf2PasswordHasher>();
            services.AddSingleton<IRegistrationTokens, RegistrationTokens>();
            services.AddSingleton<IRegistrationMailer, RegistrationMailer>();
            services.AddScoped<IRegistrationService, RegistrationService>();

            // 匿名端點的流量上限。反向代理後看到的多半是代理的 IP，因此 Register 以全域為單位限制寄信總量，
            // 同一信箱另有冷卻時間（RegistrationOptions.ResendCooldownSeconds）。
            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(RegistrationRateLimits.Register, _ =>
                    RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
                limiter.AddPolicy(RegistrationRateLimits.Verify, context =>
                    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            });

            return services;
        }
    }
}
