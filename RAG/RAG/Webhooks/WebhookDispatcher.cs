using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;

namespace RAG.Webhooks
{
    public interface IWebhookDispatcher
    {
        /// <summary>送出所有到期的通知；回傳處理的筆數</summary>
        Task<int> RunOnceAsync(CancellationToken cancellationToken = default);

        /// <summary>立刻嘗試送出一筆（測試與手動重送用）</summary>
        Task<WebhookDelivery> DeliverNowAsync(int deliveryId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 送出與重試。第一次立刻送；失敗依 1 分、5 分、30 分、2 小時、12 小時退避重試，共 6 次，
    /// 用完標記 Dead（使用者可手動重送）。成功一次就把連續失敗歸零；連續 5 筆通知都 Dead 會自動停用該 webhook。
    /// </summary>
    public sealed class WebhookDispatcher : IWebhookDispatcher
    {
        public const int MaxAttempts = 6;
        public const int DisableAfterConsecutiveDead = 5;
        public const int BatchSize = 20;
        public static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(12) };

        /// <summary>送出期間先把下次時間往後推，萬一程式在送到一半中斷，不會被另一輪立刻重複拿去送</summary>
        private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

        private readonly WenDaHubContext _db;
        private readonly IWebhookSender _sender;
        private readonly IWebhookSecretProtector _protector;
        private readonly TimeProvider _time;
        private readonly ILogger<WebhookDispatcher> _logger;

        public WebhookDispatcher(WenDaHubContext db, IWebhookSender sender, IWebhookSecretProtector protector, TimeProvider time, ILogger<WebhookDispatcher> logger)
        {
            _db = db;
            _sender = sender;
            _protector = protector;
            _time = time;
            _logger = logger;
        }

        public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var due = await _db.WebhookDelivery
                .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now && d.Webhook.IsActive)
                .OrderBy(d => d.NextAttemptAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var delivery in due) delivery.NextAttemptAt = now + Lease;
            if (due.Count > 0) await _db.SaveChangesAsync(cancellationToken);

            foreach (var delivery in due)
            {
                await AttemptAsync(delivery, cancellationToken);
            }

            return due.Count;
        }

        public async Task<WebhookDelivery> DeliverNowAsync(int deliveryId, CancellationToken cancellationToken = default)
        {
            var delivery = await _db.WebhookDelivery.FirstAsync(d => d.ID == deliveryId, cancellationToken);
            await AttemptAsync(delivery, cancellationToken);
            return delivery;
        }

        private async Task AttemptAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
        {
            var hook = await _db.Webhook.FirstAsync(w => w.ID == delivery.WebhookID, cancellationToken);
            var now = _time.GetUtcNow();

            WebhookResult result;
            try
            {
                result = await _sender.SendAsync(
                    new WebhookRequest(new Uri(hook.Url), delivery.Event, delivery.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), delivery.Payload, _protector.Unprotect(hook.SecretProtected), now),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Webhook {WebhookId} delivery {DeliveryId} threw", hook.ID, delivery.ID);
                result = new WebhookResult(null, "送出時發生未預期的錯誤");
            }

            delivery.Attempts++;
            delivery.LastStatusCode = result.StatusCode;
            delivery.LastError = result.Success ? null : result.Error;

            if (result.Success)
            {
                delivery.Status = DeliveryStatus.Succeeded;
                delivery.DeliveredAt = now.UtcDateTime;
                delivery.NextAttemptAt = null;
                hook.ConsecutiveFailures = 0;
            }
            else if (result.Permanent || delivery.Attempts >= MaxAttempts)
            {
                delivery.Status = DeliveryStatus.Dead;
                delivery.NextAttemptAt = null;
                hook.ConsecutiveFailures++;
                if (hook.IsActive && hook.ConsecutiveFailures >= DisableAfterConsecutiveDead)
                {
                    await DisableAsync(hook, $"連續 {DisableAfterConsecutiveDead} 筆通知都送不出去，已自動停用", cancellationToken);
                }
            }
            else
            {
                delivery.Status = DeliveryStatus.Pending;
                delivery.NextAttemptAt = now.UtcDateTime + RetryDelays[Math.Min(delivery.Attempts - 1, RetryDelays.Length - 1)];
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>停用：還在排隊的通知一併標記 Dead，重新啟用後不會把一堆過期事件一次補送</summary>
        public async Task DisableAsync(Webhook hook, string reason, CancellationToken cancellationToken)
        {
            hook.IsActive = false;
            hook.DisabledReason = reason;
            var pending = await _db.WebhookDelivery.Where(d => d.WebhookID == hook.ID && d.Status == DeliveryStatus.Pending).ToListAsync(cancellationToken);
            foreach (var delivery in pending)
            {
                delivery.Status = DeliveryStatus.Dead;
                delivery.NextAttemptAt = null;
                delivery.LastError ??= "Webhook 已停用";
            }
        }
    }

    /// <summary>每 5 秒送出到期的通知；每小時順便清掉 14 天前已結束的紀錄</summary>
    public sealed class WebhookDispatcherService : BackgroundService
    {
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PruneEvery = TimeSpan.FromHours(1);
        private static readonly TimeSpan Retention = TimeSpan.FromDays(14);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<WebhookDispatcherService> _logger;
        private DateTime _lastPrune = DateTime.MinValue;

        public WebhookDispatcherService(IServiceScopeFactory scopes, ILogger<WebhookDispatcherService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Tick);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>().RunOnceAsync(stoppingToken);

                    if (DateTime.UtcNow - _lastPrune > PruneEvery)
                    {
                        _lastPrune = DateTime.UtcNow;
                        var db = scope.ServiceProvider.GetRequiredService<WenDaHubContext>();
                        var cutoff = DateTime.UtcNow - Retention;
                        var old = await db.WebhookDelivery.Where(d => d.Status != DeliveryStatus.Pending && d.CreateAt < cutoff).ToListAsync(stoppingToken);
                        if (old.Count > 0)
                        {
                            db.WebhookDelivery.RemoveRange(old);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Webhook dispatcher tick failed");
                }
            }
        }
    }
}
