using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using System.Text.Json;

namespace RAG.Webhooks
{
    public interface IWebhookPublisher
    {
        /// <summary>為使用者訂閱了這個事件的 webhook 各建立一筆待送通知。失敗只記錄，不影響呼叫端的主要流程</summary>
        Task PublishAsync(int userId, int spaceId, string eventName, object data, CancellationToken cancellationToken = default);
    }

    public sealed class WebhookPublisher : IWebhookPublisher
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly WenDaHubContext _db;
        private readonly TimeProvider _time;
        private readonly ILogger<WebhookPublisher> _logger;

        public WebhookPublisher(WenDaHubContext db, TimeProvider time, ILogger<WebhookPublisher> logger)
        {
            _db = db;
            _time = time;
            _logger = logger;
        }

        public async Task PublishAsync(int userId, int spaceId, string eventName, object data, CancellationToken cancellationToken = default)
        {
            try
            {
                var hooks = await _db.Webhook.AsNoTracking().Where(w => w.UserID == userId && w.IsActive).ToListAsync(cancellationToken);
                var subscribed = hooks.Where(w => w.Events.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(eventName)).ToList();
                if (subscribed.Count == 0) return;

                var now = _time.GetUtcNow().UtcDateTime;
                foreach (var hook in subscribed)
                {
                    _db.WebhookDelivery.Add(NewDelivery(hook.ID, eventName, spaceId, data, now));
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Webhook publish failed: {Event}", eventName);
            }
        }

        /// <summary>每筆通知一個新的 EventID；payload 的 id 就是它，對方可以用來去重</summary>
        public static WebhookDelivery NewDelivery(int webhookId, string eventName, int spaceId, object data, DateTime now)
        {
            var eventId = Guid.NewGuid().ToString();
            var payload = JsonSerializer.Serialize(new { id = eventId, @event = eventName, createdAt = DateTime.SpecifyKind(now, DateTimeKind.Utc), spaceID = spaceId, data }, Json);
            return new WebhookDelivery
            {
                WebhookID = webhookId,
                EventID = eventId,
                Event = eventName,
                Payload = payload,
                Status = DeliveryStatus.Pending,
                NextAttemptAt = now,
                CreateAt = now
            };
        }
    }
}
