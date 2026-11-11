using RAG.Webhooks;
using System.Collections.Concurrent;

namespace RAG.Tests.Infrastructure
{
    /// <summary>
    /// 取代真實的 webhook 送出：記錄每個請求，並可指定回應（預設成功 200）
    /// </summary>
    public sealed class FakeWebhookSender : IWebhookSender
    {
        public ConcurrentQueue<WebhookRequest> Requests { get; } = new();

        public Func<WebhookRequest, WebhookResult> Handler { get; set; } = _ => new WebhookResult(200, null);

        public void Reset()
        {
            Requests.Clear();
            Handler = _ => new WebhookResult(200, null);
        }

        public Task<WebhookResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            return Task.FromResult(Handler(request));
        }
    }
}
