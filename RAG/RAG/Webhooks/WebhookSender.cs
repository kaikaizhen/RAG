using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Webhooks
{
    public sealed record WebhookRequest(Uri Url, string Event, string DeliveryId, string Body, string Secret, DateTimeOffset Timestamp);

    /// <summary>
    /// Permanent = 重試也不會成功（被安全規則擋下）；其餘失敗（逾時、5xx、連線失敗）會依退避時間重試
    /// </summary>
    public sealed record WebhookResult(int? StatusCode, string? Error, bool Permanent = false)
    {
        public bool Success => StatusCode is >= 200 and < 300;
    }

    public interface IWebhookSender
    {
        Task<WebhookResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken);
    }

    public static class WebhookSignature
    {
        public const string SignatureHeader = "X-RAG-Signature";
        public const string TimestampHeader = "X-RAG-Timestamp";
        public const string EventHeader = "X-RAG-Event";
        public const string DeliveryHeader = "X-RAG-Delivery";

        /// <summary>sha256=HMAC-SHA256(secret, "&lt;unix 秒&gt;.&lt;body&gt;")；對方驗證時一併檢查時間戳，可擋重放</summary>
        public static string Compute(string secret, long unixSeconds, string body)
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{unixSeconds}.{body}"));
            return "sha256=" + Convert.ToHexStringLower(hash);
        }
    }

    /// <summary>
    /// 實際送出 webhook。重點是防 SSRF：連線前自己解析 DNS，只要有任何一個結果不是公開位址就拒絕，
    /// 並且直接連到「剛檢查過的那個 IP」（不是讓 HttpClient 再解析一次），所以 DNS 重綁攻擊沒有空隙；
    /// 不跟隨重新導向，逾時 10 秒，不讀回應內容。
    /// </summary>
    public sealed class SafeWebhookSender : IWebhookSender, IDisposable
    {
        private readonly HttpClient _client;

        public SafeWebhookSender()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                ConnectCallback = ConnectToPublicAddressAsync
            };
            _client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task<WebhookResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
        {
            if (!WebhookUrlPolicy.TryValidate(request.Url.ToString(), out _, out var policyError))
            {
                return new WebhookResult(null, policyError, Permanent: true);
            }

            var unix = request.Timestamp.ToUnixTimeSeconds();
            using var message = new HttpRequestMessage(HttpMethod.Post, request.Url)
            {
                Content = new StringContent(request.Body, Encoding.UTF8, "application/json")
            };
            message.Headers.UserAgent.ParseAdd("RAG-Webhook/1.0");
            message.Headers.TryAddWithoutValidation(WebhookSignature.EventHeader, request.Event);
            message.Headers.TryAddWithoutValidation(WebhookSignature.DeliveryHeader, request.DeliveryId);
            message.Headers.TryAddWithoutValidation(WebhookSignature.TimestampHeader, unix.ToString(System.Globalization.CultureInfo.InvariantCulture));
            message.Headers.TryAddWithoutValidation(WebhookSignature.SignatureHeader, WebhookSignature.Compute(request.Secret, unix, request.Body));

            try
            {
                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var code = (int)response.StatusCode;
                return new WebhookResult(code, response.IsSuccessStatusCode ? null : $"對方回應 HTTP {code}");
            }
            catch (HttpRequestException ex) when (ex.InnerException is WebhookBlockedException blocked)
            {
                return new WebhookResult(null, blocked.Message, Permanent: true);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new WebhookResult(null, "逾時（10 秒內沒有回應）");
            }
            catch (HttpRequestException ex)
            {
                return new WebhookResult(null, $"連線失敗：{ex.InnerException?.Message ?? ex.Message}".Truncate(300));
            }
        }

        private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal) ? new[] { literal } : await Dns.GetHostAddressesAsync(host, cancellationToken);
            if (addresses.Length == 0) throw new WebhookBlockedException("網域解析不到任何位址");
            if (addresses.Any(a => !WebhookUrlPolicy.IsPublicAddress(a)))
            {
                throw new WebhookBlockedException("這個網域指向內部或保留的位址，已拒絕連線");
            }

            Exception? last = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    last = ex;
                }
            }

            throw last ?? new SocketException((int)SocketError.HostUnreachable);
        }

        public void Dispose() => _client.Dispose();
    }

    internal sealed class WebhookBlockedException : IOException
    {
        public WebhookBlockedException(string message) : base(message) { }
    }

    internal static class StringExtensions
    {
        public static string Truncate(this string value, int max) => value.Length <= max ? value : value[..max];
    }
}
