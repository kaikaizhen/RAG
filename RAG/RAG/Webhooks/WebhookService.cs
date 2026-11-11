using Library.Http;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using System.Net;
using System.Security.Cryptography;

namespace RAG.Webhooks
{
    public interface IWebhookService
    {
        Task<List<Webhook>> ListAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>建立；回傳資料列與簽章密鑰明文（只此一次）</summary>
        Task<(Webhook Webhook, string Secret)> CreateAsync(int userId, string name, string url, IReadOnlyCollection<string> events, CancellationToken cancellationToken = default);

        Task<Webhook> UpdateAsync(int userId, int id, string name, string url, IReadOnlyCollection<string> events, bool isActive, CancellationToken cancellationToken = default);

        Task DeleteAsync(int userId, int id, CancellationToken cancellationToken = default);

        /// <summary>送出測試事件並立刻回報結果</summary>
        Task<WebhookDelivery> TestAsync(int userId, int id, CancellationToken cancellationToken = default);

        Task<List<WebhookDelivery>> DeliveriesAsync(int userId, int id, CancellationToken cancellationToken = default);

        /// <summary>手動重送一筆通知（重試次數歸零，立刻送）</summary>
        Task<WebhookDelivery> RedeliverAsync(int userId, int id, int deliveryId, CancellationToken cancellationToken = default);
    }

    public sealed class WebhookService : IWebhookService
    {
        public const int MaxPerUser = 20;
        public const int DeliveryListLimit = 50;
        public const string SecretPrefix = "whsec_";

        private readonly WenDaHubContext _db;
        private readonly IWebhookSecretProtector _protector;
        private readonly WebhookDispatcher _dispatcher;
        private readonly TimeProvider _time;

        public WebhookService(WenDaHubContext db, IWebhookSecretProtector protector, WebhookDispatcher dispatcher, TimeProvider time)
        {
            _db = db;
            _protector = protector;
            _dispatcher = dispatcher;
            _time = time;
        }

        public Task<List<Webhook>> ListAsync(int userId, CancellationToken cancellationToken = default) =>
            _db.Webhook.AsNoTracking().Where(w => w.UserID == userId).OrderBy(w => w.ID).ToListAsync(cancellationToken);

        public async Task<(Webhook Webhook, string Secret)> CreateAsync(int userId, string name, string url, IReadOnlyCollection<string> events, CancellationToken cancellationToken = default)
        {
            var uri = ValidateUrl(url);
            var eventList = ValidateEvents(events);
            if (await _db.Webhook.CountAsync(w => w.UserID == userId, cancellationToken) >= MaxPerUser)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"最多只能設定 {MaxPerUser} 個 webhook");
            }

            var secret = SecretPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hook = new Webhook
            {
                UserID = userId,
                Name = name.Trim(),
                Url = uri.ToString(),
                SecretProtected = _protector.Protect(secret),
                Events = string.Join(',', eventList),
                IsActive = true,
                CreateAt = _time.GetUtcNow().UtcDateTime
            };
            _db.Webhook.Add(hook);
            await _db.SaveChangesAsync(cancellationToken);
            return (hook, secret);
        }

        public async Task<Webhook> UpdateAsync(int userId, int id, string name, string url, IReadOnlyCollection<string> events, bool isActive, CancellationToken cancellationToken = default)
        {
            var hook = await GetOwnedAsync(userId, id, cancellationToken);
            var uri = ValidateUrl(url);
            var eventList = ValidateEvents(events);

            hook.Name = name.Trim();
            hook.Url = uri.ToString();
            hook.Events = string.Join(',', eventList);

            if (isActive && !hook.IsActive)
            {
                hook.IsActive = true;
                hook.ConsecutiveFailures = 0;
                hook.DisabledReason = null;
            }
            else if (!isActive && hook.IsActive)
            {
                await _dispatcher.DisableAsync(hook, "已由你停用", cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return hook;
        }

        public async Task DeleteAsync(int userId, int id, CancellationToken cancellationToken = default)
        {
            var hook = await GetOwnedAsync(userId, id, cancellationToken);
            _db.Webhook.Remove(hook);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<WebhookDelivery> TestAsync(int userId, int id, CancellationToken cancellationToken = default)
        {
            var hook = await GetOwnedAsync(userId, id, cancellationToken);
            var delivery = WebhookPublisher.NewDelivery(hook.ID, WebhookEvents.Ping, spaceId: 0, new { message = "這是一則測試事件" }, _time.GetUtcNow().UtcDateTime);
            _db.WebhookDelivery.Add(delivery);
            await _db.SaveChangesAsync(cancellationToken);
            return await _dispatcher.DeliverNowAsync(delivery.ID, cancellationToken);
        }

        public async Task<List<WebhookDelivery>> DeliveriesAsync(int userId, int id, CancellationToken cancellationToken = default)
        {
            await GetOwnedAsync(userId, id, cancellationToken);
            return await _db.WebhookDelivery.AsNoTracking()
                .Where(d => d.WebhookID == id)
                .OrderByDescending(d => d.CreateAt).ThenByDescending(d => d.ID)
                .Take(DeliveryListLimit)
                .ToListAsync(cancellationToken);
        }

        public async Task<WebhookDelivery> RedeliverAsync(int userId, int id, int deliveryId, CancellationToken cancellationToken = default)
        {
            var hook = await GetOwnedAsync(userId, id, cancellationToken);
            if (!hook.IsActive) throw new HttpException(HttpStatusCode.Conflict, "這個 webhook 已停用，請先重新啟用");

            var delivery = await _db.WebhookDelivery.FirstOrDefaultAsync(d => d.ID == deliveryId && d.WebhookID == id, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此通知");

            delivery.Status = DeliveryStatus.Pending;
            delivery.Attempts = 0;
            delivery.NextAttemptAt = _time.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            return await _dispatcher.DeliverNowAsync(delivery.ID, cancellationToken);
        }

        private async Task<Webhook> GetOwnedAsync(int userId, int id, CancellationToken cancellationToken) =>
            await _db.Webhook.FirstOrDefaultAsync(w => w.ID == id && w.UserID == userId, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此 webhook");

        private static Uri ValidateUrl(string url) =>
            WebhookUrlPolicy.TryValidate(url, out var uri, out var error) ? uri! : throw new HttpException(HttpStatusCode.BadRequest, error);

        private static List<string> ValidateEvents(IReadOnlyCollection<string> events)
        {
            var distinct = events.Distinct().ToList();
            if (distinct.Count == 0) throw new HttpException(HttpStatusCode.BadRequest, "請至少訂閱一個事件");
            var unknown = distinct.Where(e => !WebhookEvents.IsSubscribable(e)).ToList();
            if (unknown.Count > 0) throw new HttpException(HttpStatusCode.BadRequest, $"不支援的事件：{string.Join(", ", unknown)}");
            return distinct;
        }
    }

    public static class WebhookExtensions
    {
        public static IServiceCollection AddWebhooks(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IWebhookSecretProtector, WebhookSecretProtector>();
            services.AddSingleton<IWebhookSender, SafeWebhookSender>();
            services.AddScoped<WebhookDispatcher>();
            services.AddScoped<IWebhookDispatcher>(sp => sp.GetRequiredService<WebhookDispatcher>());
            services.AddScoped<IWebhookPublisher, WebhookPublisher>();
            services.AddScoped<IWebhookService, WebhookService>();

            // 背景送出可以用 Webhooks:DispatcherEnabled=false 關閉（測試用；多個執行個體時也只讓一個開啟）
            if (configuration.GetValue("Webhooks:DispatcherEnabled", true))
            {
                services.AddHostedService<WebhookDispatcherService>();
            }

            return services;
        }
    }
}
