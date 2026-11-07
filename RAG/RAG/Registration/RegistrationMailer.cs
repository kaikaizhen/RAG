using System.Net;
using Library.Mail;

namespace RAG.Registration
{
    public interface IRegistrationMailer
    {
        /// <summary>
        /// 是否已設定可用的寄信服務（未設定時不開放註冊，而不是建立永遠無法驗證的流程）
        /// </summary>
        bool Available { get; }

        Task SendVerificationAsync(string email, string name, string verifyLink, TimeSpan validFor, CancellationToken cancellationToken = default);

        /// <summary>
        /// 該信箱已註冊時寄出的通知（不在 API 回應中透露，避免被用來探測帳號）
        /// </summary>
        Task SendAlreadyRegisteredAsync(string email, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 寄出驗證信 / 已註冊通知。信件版型：頂端 logo、標題、內文、按鈕、備用網址、頁尾。
    /// 為了相容各家信箱（尤其 Outlook 不支援外部 CSS），一律使用 table 版面與行內樣式；
    /// logo 必須是公開網址（Gmail 不顯示內嵌圖），因此取自前端網站的靜態圖示，網址由 VerifyUrlBase 推算。
    /// </summary>
    public sealed class RegistrationMailer : IRegistrationMailer
    {
        // 透明背景的線稿 logo（App 圖示是白底、有雜訊，放在白色卡片上會看到淡淡的方框）
        private const string LogoPath = "/logo-mark.png";

        private readonly IMailService? _mail;
        private readonly RegistrationOptions _options;

        public RegistrationMailer(IServiceProvider services, RegistrationOptions options)
        {
            // IMailService 只有在 Library:Mail 設定存在時才會註冊
            _mail = services.GetService(typeof(IMailService)) as IMailService;
            _options = options;
        }

        public bool Available => _mail is not null;

        public Task SendVerificationAsync(string email, string name, string verifyLink, TimeSpan validFor, CancellationToken cancellationToken = default)
        {
            var hours = (int)Math.Ceiling(validFor.TotalHours);
            var body = Layout(
                preheader: $"點擊連結完成信箱驗證，{hours} 小時內有效。",
                heading: "請驗證您的信箱",
                content: $"""
                    <p style="margin:0 0 12px;">{Encode(name)} 您好，</p>
                    <p style="margin:0 0 24px;">感謝註冊 {Encode(_options.ServiceName)}。請點擊下方按鈕完成信箱驗證並啟用帳號（{hours} 小時內有效）。</p>
                    {Button(verifyLink, "驗證信箱並啟用帳號")}
                    <p style="margin:24px 0 0;font-size:13px;color:#6b7280;">如果按鈕無法點擊，請複製以下網址到瀏覽器開啟：</p>
                    <p style="margin:6px 0 0;font-size:12px;line-height:1.5;color:#9ca3af;word-break:break-all;">{Encode(verifyLink)}</p>
                    <p style="margin:24px 0 0;font-size:13px;color:#6b7280;">如果您沒有註冊過，請忽略這封信，不會建立任何帳號。</p>
                    """);
            return Send(email, $"請驗證您的 {_options.ServiceName} 信箱", body, cancellationToken);
        }

        public Task SendAlreadyRegisteredAsync(string email, CancellationToken cancellationToken = default)
        {
            var body = Layout(
                preheader: "此信箱已經有帳號，請直接登入。",
                heading: "此信箱已經註冊過了",
                content: $"""
                    <p style="margin:0 0 12px;">您好，</p>
                    <p style="margin:0 0 24px;">有人嘗試以這個信箱註冊 {Encode(_options.ServiceName)}，但此信箱已經有帳號了，請直接登入；如果忘記密碼，請聯絡管理員。</p>
                    {Button(SiteLink("/login"), "前往登入")}
                    <p style="margin:24px 0 0;font-size:13px;color:#6b7280;">如果不是您本人操作，請忽略這封信，您的帳號不會有任何變動。</p>
                    """);
            return Send(email, $"此信箱已註冊 {_options.ServiceName}", body, cancellationToken);
        }

        private string Layout(string preheader, string heading, string content)
        {
            var service = Encode(_options.ServiceName);
            var logo = Encode(SiteLink(LogoPath));
            return $"""
                <!doctype html>
                <html lang="zh-TW">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="color-scheme" content="light">
                <title>{Encode(heading)}</title>
                </head>
                <body style="margin:0;padding:0;background:#f4f4f5;">
                <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{Encode(preheader)}</div>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f4f4f5;">
                <tr><td align="center" style="padding:32px 16px;">
                  <table role="presentation" width="560" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:560px;background:#ffffff;border-radius:16px;border:1px solid #e5e7eb;">
                    <tr><td align="center" style="padding:32px 32px 8px;">
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 auto;"><tr><td bgcolor="#ffffff" style="background:#ffffff;border-radius:20px;padding:6px;">
                      <img src="{logo}" width="72" height="72" alt="{service}" style="display:block;border:0;width:72px;height:72px;">
                      </td></tr></table>
                      <div style="margin-top:8px;font-family:-apple-system,'Segoe UI','PingFang TC','Noto Sans TC','Microsoft JhengHei',Arial,sans-serif;font-size:18px;font-weight:700;color:#111827;">{service}</div>
                    </td></tr>
                    <tr><td style="padding:16px 40px 40px;font-family:-apple-system,'Segoe UI','PingFang TC','Noto Sans TC','Microsoft JhengHei',Arial,sans-serif;font-size:15px;line-height:1.7;color:#374151;">
                      <h1 style="margin:0 0 20px;text-align:center;font-size:22px;line-height:1.4;color:#111827;">{Encode(heading)}</h1>
                      {content}
                    </td></tr>
                  </table>
                  <p style="margin:16px 0 0;font-family:-apple-system,'Segoe UI','PingFang TC','Noto Sans TC','Microsoft JhengHei',Arial,sans-serif;font-size:12px;line-height:1.6;color:#9ca3af;">這是 {service} 系統自動寄出的信件，請勿直接回覆。</p>
                </td></tr>
                </table>
                </body>
                </html>
                """;
        }

        /// <summary>
        /// 相容各家信箱的按鈕（table + 行內樣式；Outlook 不支援 padding 在 a 上的圓角，退化成方形仍可點）
        /// </summary>
        private static string Button(string href, string label) => $"""
            <table role="presentation" align="center" cellpadding="0" cellspacing="0" border="0" style="margin:0 auto;">
            <tr><td align="center" bgcolor="#111827" style="border-radius:10px;">
            <a href="{Encode(href)}" style="display:inline-block;padding:14px 32px;font-family:-apple-system,'Segoe UI','PingFang TC','Noto Sans TC','Microsoft JhengHei',Arial,sans-serif;font-size:16px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">{Encode(label)}</a>
            </td></tr>
            </table>
            """;

        /// <summary>
        /// 前端網站上的絕對網址（由 VerifyUrlBase 的來源推算）
        /// </summary>
        private string SiteLink(string path) =>
            Uri.TryCreate(_options.VerifyUrlBase, UriKind.Absolute, out var verify)
                ? new Uri(new Uri(verify.GetLeftPart(UriPartial.Authority)), path).ToString()
                : path;

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

        private Task Send(string email, string subject, string body, CancellationToken cancellationToken) =>
            _mail is null
                ? throw new InvalidOperationException("Mail service is not configured.")
                : _mail.SendAsync(new MailMessageRequest([email], subject, body) { IsHtml = true }, cancellationToken);
    }
}
