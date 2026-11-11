using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace RAG.Inbox
{
    public static class InboxRateLimits
    {
        public const string Write = "inbox-write";
    }

    public static class InboxExtensions
    {
        public static IServiceCollection AddInbox(this IServiceCollection services)
        {
            services.AddScoped<IInboxService, InboxService>();

            // 暫存區寫入：每個使用者每分鐘 60 次（權杖與登入共用額度，避免用多把權杖繞過）
            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.AddPolicy(InboxRateLimits.Write, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            return services;
        }
    }
}
