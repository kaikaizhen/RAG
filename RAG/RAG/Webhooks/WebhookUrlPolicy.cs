using System.Net;
using System.Net.Sockets;

namespace RAG.Webhooks
{
    /// <summary>
    /// Webhook 網址的安全規則。伺服器會代使用者發出請求，所以必須防止 SSRF：
    /// 只允許 https，且目標必須是公開網路位址；不允許 localhost / 私有網段 / 雲端中繼位址。
    /// 設定時檢查一次（讓使用者即時看到錯誤），實際送出時還會對解析出的 IP 再檢查一次（防 DNS 重綁，見 SafeWebhookSender）。
    /// </summary>
    public static class WebhookUrlPolicy
    {
        public const int MaxLength = 500;

        private static readonly string[] BlockedSuffixes = { ".localhost", ".local", ".internal", ".lan", ".home", ".intranet" };

        public static bool TryValidate(string? url, out Uri? uri, out string error)
        {
            uri = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(url) || url.Length > MaxLength)
            {
                error = $"網址必填，且不能超過 {MaxLength} 個字元";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
            {
                error = "網址格式不正確";
                return false;
            }

            if (parsed.Scheme != Uri.UriSchemeHttps)
            {
                error = "Webhook 一律要使用 https://";
                return false;
            }

            if (!string.IsNullOrEmpty(parsed.UserInfo))
            {
                error = "網址不能包含帳號密碼";
                return false;
            }

            var host = parsed.IdnHost.TrimEnd('.').ToLowerInvariant();
            if (host.Length == 0 || host == "localhost" || BlockedSuffixes.Any(host.EndsWith))
            {
                error = "不能使用 localhost 或內部網域；Webhook 由伺服器發出，只能連到公開網路上的 https 網址";
                return false;
            }

            if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
            {
                if (!IsPublicAddress(literal))
                {
                    error = "不能使用內部或保留的 IP 位址";
                    return false;
                }
            }
            else if (!host.Contains('.'))
            {
                error = "請使用完整的網域名稱";
                return false;
            }

            uri = parsed;
            return true;
        }

        /// <summary>是否為可以對外連線的公開位址（非私有、迴圈、連結本地、保留、多播）</summary>
        public static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                return !(
                    b[0] == 0 ||                                   // 0.0.0.0/8
                    b[0] == 10 ||                                  // 10/8
                    (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||  // 100.64/10 CGNAT
                    b[0] == 127 ||                                 // loopback
                    (b[0] == 169 && b[1] == 254) ||                // link-local（含雲端中繼 169.254.169.254）
                    (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||   // 172.16/12
                    (b[0] == 192 && b[1] == 0 && b[2] == 0) ||     // 192.0.0.0/24
                    (b[0] == 192 && b[1] == 0 && b[2] == 2) ||     // TEST-NET-1
                    (b[0] == 192 && b[1] == 168) ||                // 192.168/16
                    (b[0] == 198 && (b[1] == 18 || b[1] == 19)) || // 198.18/15
                    (b[0] == 198 && b[1] == 51 && b[2] == 100) ||  // TEST-NET-2
                    (b[0] == 203 && b[1] == 0 && b[2] == 113) ||   // TEST-NET-3
                    b[0] >= 224);                                  // 多播與保留
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any)) return false;
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal) return false;

                var b = address.GetAddressBytes();
                if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false; // 2001:db8::/32 文件用
                // 6to4（2002::/16）與 NAT64（64:ff9b::/96）把 IPv4 嵌在裡面：連嵌入的 IPv4 一起檢查
                if (b[0] == 0x20 && b[1] == 0x02) return IsPublicAddress(new IPAddress(new[] { b[2], b[3], b[4], b[5] }));
                if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b) return IsPublicAddress(new IPAddress(new[] { b[12], b[13], b[14], b[15] }));
                return true;
            }

            return false;
        }
    }
}
