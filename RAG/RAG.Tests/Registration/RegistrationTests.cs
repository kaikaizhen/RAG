using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Library.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAG.Authentication;
using RAG.Registration;
using RAG.Tests.Infrastructure;

namespace RAG.Tests.Registration
{
    public sealed class FakeMailService : IMailService
    {
        public List<MailMessageRequest> Sent { get; } = new();

        public bool Fail { get; set; }

        public Task SendAsync(MailMessageRequest message, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new MailDeliveryException("fail", new Exception("smtp down"));
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 啟用自行註冊，並以假的寄信服務取代 Gmail SMTP
    /// </summary>
    public sealed class RegistrationApiFactory : RagApiFactory
    {
        public FakeMailService Mail { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Registration:Enabled", "true");
            builder.UseSetting("Registration:VerifyUrlBase", "https://web.test/verify-email");
            builder.UseSetting("Registration:ResendCooldownSeconds", "60");
            builder.UseSetting("Library:Mail:Host", "smtp.test");
            builder.UseSetting("Library:Mail:Account", "sender@test");
            builder.UseSetting("Library:Mail:Password", "not-a-real-password");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMailService>();
                services.AddSingleton<IMailService>(Mail);
            });
        }
    }

    public class RegistrationTests : IClassFixture<RegistrationApiFactory>, IAsyncLifetime
    {
        private const string Password = "correct-horse-battery";
        private readonly RegistrationApiFactory _factory;
        private readonly HttpClient _anonymous;

        public RegistrationTests(RegistrationApiFactory factory)
        {
            _factory = factory;
            _anonymous = factory.CreateClientWithToken(null);
        }

        public Task InitializeAsync()
        {
            _factory.Mail.Sent.Clear();
            _factory.Mail.Fail = false;
            return SeedData.ResetAsync(_factory);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

        private Task<HttpResponseMessage> RegisterAsync(string email, string? password = null, string? name = null) =>
            _anonymous.PostAsJsonAsync("/api/v1/Auth/Register", new { Account = email, Password = password ?? Password, NameZh = name ?? "小明" });

        private string TokenFromLastMail()
        {
            var body = _factory.Mail.Sent[^1].Body;
            return Regex.Match(body, @"token=([A-Za-z0-9_\-%.]+)").Groups[1].Value;
        }

        private Task<HttpResponseMessage> VerifyAsync(string token) =>
            _anonymous.PostAsJsonAsync("/api/v1/Auth/VerifyEmail", new { Token = token });

        private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
            _anonymous.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = email, Password = password });

        private Task<bool> UserExistsAsync(string email) =>
            _factory.WithDbAsync(db => db.UserCache.AsNoTracking().AnyAsync(u => u.Account == email));

        private record LoginResult(int UserID, string AccessToken);
        private record SpaceItem(int SpaceID, string Key, string Role, bool IsDefault);

        [Fact]
        public async Task Register_sends_verification_mail_but_does_not_create_account_yet()
        {
            var email = NewEmail();

            var response = await RegisterAsync(email);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var mail = Assert.Single(_factory.Mail.Sent);
            Assert.Equal(new[] { email }, mail.To);
            Assert.StartsWith("https://web.test/verify-email?token=", Regex.Match(mail.Body, @"https://web\.test/verify-email\?token=[^\s""<]+").Value);
            // 點擊連結前不存在帳號，也就不可能被搶先佔用或登入
            Assert.False(await UserExistsAsync(email));
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, Password)).StatusCode);
        }

        [Fact]
        public async Task Verification_mail_is_branded_with_logo_button_and_fallback_link()
        {
            var email = NewEmail();

            await RegisterAsync(email, name: "<b>小明</b>");

            var mail = Assert.Single(_factory.Mail.Sent);
            Assert.True(mail.IsHtml);
            // logo 取自前端網站（由 VerifyUrlBase 推算），Gmail 不顯示內嵌圖，必須是公開網址
            Assert.Contains("src=\"https://web.test/logo-mark.png\"", mail.Body);
            // 按鈕與備用網址都指向驗證連結
            Assert.Equal(2, Regex.Matches(mail.Body, @"https://web\.test/verify-email\?token=").Count);
            Assert.Contains("驗證信箱並啟用帳號", mail.Body);
            // 使用者輸入的名稱必須轉義，不能在信件中注入 HTML
            Assert.DoesNotContain("<b>小明</b>", mail.Body);
            Assert.Contains("&lt;b&gt;小明&lt;/b&gt;", mail.Body);
            DumpForPreview("verification", mail);
        }

        [Fact]
        public async Task Already_registered_mail_has_a_login_button_and_no_verification_link()
        {
            var email = NewEmail();
            await RegisterAsync(email);
            await VerifyAsync(TokenFromLastMail());
            _factory.Mail.Sent.Clear();
            // 冷卻期內同一信箱不會再寄信；換成不同大小寫仍是同一信箱，這裡直接清掉冷卻以驗證內容
            _factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove($"register:{email}");

            await RegisterAsync(email);

            var mail = Assert.Single(_factory.Mail.Sent);
            Assert.Contains("href=\"https://web.test/login\"", mail.Body);
            Assert.DoesNotContain("verify-email", mail.Body);
            DumpForPreview("already-registered", mail);
        }

        /// <summary>
        /// 設定環境變數 RAG_DUMP_MAIL=目錄 時，把信件輸出成 .html 供預覽（一般測試不會寫檔）
        /// </summary>
        private static void DumpForPreview(string name, MailMessageRequest mail)
        {
            var dir = Environment.GetEnvironmentVariable("RAG_DUMP_MAIL");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{name}.subject.txt"), mail.Subject);
            File.WriteAllText(Path.Combine(dir, $"{name}.html"), mail.Body);
        }

        [Fact]
        public async Task Verify_creates_account_with_hashed_password_and_own_space_then_login_works()
        {
            var email = NewEmail();
            await RegisterAsync(email, name: "小明");

            var verify = await VerifyAsync(TokenFromLastMail());

            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
            var user = await _factory.WithDbAsync(db => db.UserCache.AsNoTracking().SingleAsync(u => u.Account == email));
            Assert.Equal("free", user.Manage);
            Assert.True(user.IsActive);
            Assert.DoesNotContain(Password, user.Password);
            Assert.StartsWith("pbkdf2-sha512$", user.Password);

            var login = await LoginAsync(email, Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var session = (await login.Content.ReadFromJsonAsync<LoginResult>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;

            var client = _factory.CreateClientWithToken(session.AccessToken);
            var spaces = (await client.GetFromJsonAsync<List<SpaceItem>>("/api/v1/Space", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
            var own = Assert.Single(spaces);
            Assert.Equal($"u-{user.ID}", own.Key);
            Assert.Equal("owner", own.Role);
            Assert.True(own.IsDefault);
            // 不屬於預設 Space，也看不到其他人的 Space
            Assert.NotEqual(SeedData.DefaultSpace, own.SpaceID);
        }

        [Fact]
        public async Task Two_registered_users_get_separate_spaces()
        {
            var first = NewEmail();
            var second = NewEmail();
            await RegisterAsync(first);
            await VerifyAsync(TokenFromLastMail());
            await RegisterAsync(second);
            await VerifyAsync(TokenFromLastMail());

            var keys = await _factory.WithDbAsync(db => db.AccountSpace.AsNoTracking()
                .Where(a => a.User.Account == first || a.User.Account == second)
                .Select(a => a.SpaceID).ToListAsync());

            Assert.Equal(2, keys.Distinct().Count());
        }

        [Fact]
        public async Task Verifying_the_same_link_twice_is_harmless()
        {
            var email = NewEmail();
            await RegisterAsync(email);
            var token = TokenFromLastMail();

            Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(token)).StatusCode);

            Assert.Equal(1, await _factory.WithDbAsync(db => db.UserCache.CountAsync(u => u.Account == email)));
            Assert.Equal(1, await _factory.WithDbAsync(db => db.AccountSpace.CountAsync(a => a.User.Account == email)));
        }

        [Fact]
        public async Task Registering_an_existing_email_looks_identical_and_never_touches_the_account()
        {
            var email = NewEmail();
            await RegisterAsync(email);
            await VerifyAsync(TokenFromLastMail());
            var before = await _factory.WithDbAsync(db => db.UserCache.AsNoTracking().SingleAsync(u => u.Account == email));
            _factory.Mail.Sent.Clear();

            // 另一個人用別人的信箱註冊並想設定自己的密碼：回應與一般註冊相同，但不影響既有帳號
            var attempt = await RegisterAsync(email, password: "attacker-password-1");
            await Task.Delay(0);

            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
            var after = await _factory.WithDbAsync(db => db.UserCache.AsNoTracking().SingleAsync(u => u.Account == email));
            Assert.Equal(before.Password, after.Password);
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, "attacker-password-1")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, Password)).StatusCode);
        }

        [Fact]
        public async Task Same_email_within_cooldown_sends_only_one_mail()
        {
            var email = NewEmail();

            await RegisterAsync(email);
            var again = await RegisterAsync(email);

            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Single(_factory.Mail.Sent);
        }

        [Fact]
        public async Task Mail_failure_returns_503_and_does_not_block_a_retry()
        {
            var email = NewEmail();
            _factory.Mail.Fail = true;

            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RegisterAsync(email)).StatusCode);

            _factory.Mail.Fail = false;
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(email)).StatusCode);
            Assert.Single(_factory.Mail.Sent);
        }

        [Theory]
        [InlineData("not-an-email", "long-enough-pw")]
        [InlineData("a@b", "long-enough-pw")]
        [InlineData("使用者@example.com", "long-enough-pw")]
        [InlineData("two@example.com, three@example.com", "long-enough-pw")]
        [InlineData("ok@example.com", "short")]
        public async Task Invalid_input_is_rejected(string email, string password)
        {
            var response = await RegisterAsync(email, password);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_factory.Mail.Sent);
        }

        [Theory]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("aaaa.bbbb")]
        public async Task Bad_tokens_are_rejected(string token)
        {
            var response = await VerifyAsync(token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Tampered_token_is_rejected()
        {
            var email = NewEmail();
            await RegisterAsync(email);
            var token = TokenFromLastMail();
            var tampered = token[..^2] + (token[^2] == 'A' ? "B" : "A") + token[^1];

            Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(tampered)).StatusCode);
            Assert.False(await UserExistsAsync(email));
        }

        [Fact]
        public async Task Registration_token_is_not_usable_as_an_access_token()
        {
            await RegisterAsync(NewEmail());
            var client = _factory.CreateClientWithToken(TokenFromLastMail());

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/Space")).StatusCode);
        }
    }

    public class RegistrationDisabledTests : IClassFixture<RagApiFactory>
    {
        private readonly RagApiFactory _factory;

        public RegistrationDisabledTests(RagApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Registration_is_closed_unless_explicitly_enabled()
        {
            var client = _factory.CreateClientWithToken(null);

            var register = await client.PostAsJsonAsync("/api/v1/Auth/Register", new { Account = "x@example.com", Password = "long-enough-pw" });
            var verify = await client.PostAsJsonAsync("/api/v1/Auth/VerifyEmail", new { Token = "anything" });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, register.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, verify.StatusCode);
        }

        [Fact]
        public async Task Anonymous_user_creation_through_the_user_endpoint_stays_closed()
        {
            var client = _factory.CreateClientWithToken(null);

            var response = await client.PostAsJsonAsync("/api/v1/User", new { Account = "x@example.com", Password = "long-enough-pw" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class PasswordHashingTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;

        public PasswordHashingTests(RagApiFactory factory) => _factory = factory;

        public Task InitializeAsync() => SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Legacy_plaintext_password_still_logs_in_and_is_upgraded_to_a_hash()
        {
            var client = _factory.CreateClientWithToken(null);

            var first = await client.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = SeedData.UserAAccount, Password = SeedData.UserAPassword });

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var stored = (await SeedData.GetUserAsync(_factory, SeedData.UserA))!.Password;
            Assert.StartsWith("pbkdf2-sha512$", stored);
            Assert.DoesNotContain(SeedData.UserAPassword, stored);

            var second = await client.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = SeedData.UserAAccount, Password = SeedData.UserAPassword });
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = SeedData.UserAAccount, Password = "wrong" })).StatusCode);
        }

        [Fact]
        public async Task Unknown_account_and_wrong_password_look_the_same()
        {
            var client = _factory.CreateClientWithToken(null);

            var unknown = await client.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = "nobody", Password = "whatever" });
            var wrong = await client.PostAsJsonAsync("/api/v1/Auth/Login", new { Account = SeedData.UserAAccount, Password = "wrong" });

            Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            // 除了每次不同的 traceId，回應內容必須相同
            Assert.Equal(await WithoutTraceId(unknown), await WithoutTraceId(wrong));
        }

        private static async Task<string> WithoutTraceId(HttpResponseMessage response)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            node.Remove("traceId");
            return node.ToJsonString();
        }

        [Fact]
        public void Hasher_round_trips_rejects_wrong_passwords_and_oversized_input()
        {
            var hasher = new Pbkdf2PasswordHasher();
            var hash = hasher.Hash("s3cret-password");

            Assert.True(hasher.Verify("s3cret-password", hash, out var upgrade));
            Assert.False(upgrade);
            Assert.False(hasher.Verify("other", hash, out _));
            Assert.NotEqual(hash, hasher.Hash("s3cret-password"));
            Assert.False(hasher.Verify(new string('a', Pbkdf2PasswordHasher.MaxPasswordLength + 1), hash, out _));
        }
    }

    public class RegistrationTokenTests
    {
        private sealed class TestTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static (RegistrationTokens tokens, TestTime time) Create(byte[]? key = null)
        {
            var time = new TestTime();
            var jwt = new JwtOptions { SigningKey = key ?? RandomNumberGenerator.GetBytes(32) };
            return (new RegistrationTokens(jwt, time), time);
        }

        private static readonly RegistrationIntent Intent = new("a@example.com", "hash", "甲", "A", "A");

        [Fact]
        public void Round_trips_and_does_not_expose_contents()
        {
            var (tokens, _) = Create();

            var token = tokens.Protect(Intent, TimeSpan.FromHours(1));

            Assert.True(tokens.TryUnprotect(token, out var result));
            Assert.Equal(Intent, result);
            Assert.DoesNotContain("example", token);
            Assert.DoesNotContain("hash", token);
        }

        [Fact]
        public void Real_password_hash_round_trips_and_the_link_is_short()
        {
            var (tokens, _) = Create();
            var intent = new RegistrationIntent("someone@example.com", new Pbkdf2PasswordHasher().Hash("correct-horse-battery"), "小明", "小明", "小明");

            var token = tokens.Protect(intent, TimeSpan.FromHours(24));

            Assert.True(tokens.TryUnprotect(token, out var result));
            Assert.Equal(intent, result);
            // 備用網址顯示在信件中，不能太長（改為二進位格式前約 420 字元）
            Assert.InRange(token.Length, 1, 200);
            // 密碼雜湊也能用來驗證密碼，代表還原是無損的
            Assert.True(new Pbkdf2PasswordHasher().Verify("correct-horse-battery", result.PasswordHash, out _));
        }

        [Fact]
        public void Different_names_are_all_preserved()
        {
            var (tokens, _) = Create();
            var intent = new RegistrationIntent("a@example.com", new Pbkdf2PasswordHasher().Hash("pw-123456"), "小明", "Ming", "ミン");

            Assert.True(tokens.TryUnprotect(tokens.Protect(intent, TimeSpan.FromHours(1)), out var result));
            Assert.Equal(intent, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("AAAA")]
        [InlineData("!!!not-base64!!!")]
        public void Garbage_and_truncated_tokens_are_rejected(string garbage)
        {
            var (tokens, _) = Create();
            var valid = tokens.Protect(Intent, TimeSpan.FromHours(1));

            Assert.False(tokens.TryUnprotect(garbage, out _));
            Assert.False(tokens.TryUnprotect(valid[..^6], out _));
            Assert.False(tokens.TryUnprotect(valid + "AA", out _));
        }

        [Fact]
        public void Expires()
        {
            var (tokens, time) = Create();
            var token = tokens.Protect(Intent, TimeSpan.FromHours(1));

            time.Now = time.Now.AddHours(1).AddSeconds(1);

            Assert.False(tokens.TryUnprotect(token, out _));
        }

        [Fact]
        public void A_different_signing_key_cannot_read_it()
        {
            var (issuer, _) = Create();
            var (other, _) = Create();

            Assert.False(other.TryUnprotect(issuer.Protect(Intent, TimeSpan.FromHours(1)), out _));
        }
    }
}
