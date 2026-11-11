using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using RAG.Webhooks;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RAG.Tests.Platform
{
    public class WebhookUrlPolicyTests
    {
        [Theory]
        [InlineData("https://example.com/hook")]
        [InlineData("https://hooks.example.com:8443/a?b=1")]
        [InlineData("https://8.8.8.8/hook")]
        public void Public_https_urls_are_allowed(string url) => Assert.True(WebhookUrlPolicy.TryValidate(url, out _, out _));

        [Theory]
        [InlineData("http://example.com/hook")]                // 不是 https
        [InlineData("ftp://example.com/hook")]
        [InlineData("https://localhost/hook")]
        [InlineData("https://app.localhost/hook")]
        [InlineData("https://127.0.0.1/hook")]
        [InlineData("https://10.0.0.5/hook")]
        [InlineData("https://192.168.0.1/hook")]
        [InlineData("https://172.16.0.1/hook")]
        [InlineData("https://169.254.169.254/latest/meta-data")] // 雲端中繼位址
        [InlineData("https://100.64.0.1/hook")]
        [InlineData("https://[::1]/hook")]
        [InlineData("https://[fe80::1]/hook")]
        [InlineData("https://[fd00::1]/hook")]
        [InlineData("https://[::ffff:127.0.0.1]/hook")]        // IPv4-mapped
        [InlineData("https://[2002:7f00:0001::1]/hook")]       // 6to4 嵌入 127.0.0.1
        [InlineData("https://user:pass@example.com/hook")]
        [InlineData("https://nas.internal/hook")]
        [InlineData("https://nas.local/hook")]
        [InlineData("https://singlelabel/hook")]
        [InlineData("not a url")]
        [InlineData("")]
        public void Unsafe_urls_are_rejected(string url) => Assert.False(WebhookUrlPolicy.TryValidate(url, out _, out var error), "should reject: " + url + " " + error);

        [Fact]
        public void Overlong_urls_are_rejected() => Assert.False(WebhookUrlPolicy.TryValidate("https://example.com/" + new string('a', 600), out _, out _));

        [Theory]
        [InlineData("8.8.8.8", true)]
        [InlineData("1.1.1.1", true)]
        [InlineData("172.32.0.1", true)]
        [InlineData("172.15.255.255", true)]
        [InlineData("0.0.0.0", false)]
        [InlineData("255.255.255.255", false)]
        [InlineData("224.0.0.1", false)]
        [InlineData("2606:4700:4700::1111", true)]
        [InlineData("2001:db8::1", false)]
        public void Address_classification(string ip, bool expected) => Assert.Equal(expected, WebhookUrlPolicy.IsPublicAddress(IPAddress.Parse(ip)));

        [Fact]
        public async Task The_real_sender_refuses_internal_targets_without_connecting()
        {
            using var sender = new SafeWebhookSender();

            var result = await sender.SendAsync(new WebhookRequest(new Uri("https://127.0.0.1:9/hook"), "ping", "1", "{}", "secret", DateTimeOffset.UtcNow), CancellationToken.None);

            Assert.True(result.Permanent);
            Assert.False(result.Success);
        }

        [Fact]
        public void Signature_matches_the_documented_scheme()
        {
            // sha256=HMAC-SHA256(secret, "<unix>.<body>")
            var expected = "sha256=" + Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("s3cret"), System.Text.Encoding.UTF8.GetBytes("1700000000.{\"a\":1}")));

            Assert.Equal(expected, WebhookSignature.Compute("s3cret", 1700000000, "{\"a\":1}"));
        }
    }

    public class WebhookTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const int Space1 = SeedData.DefaultSpace;
        private const string HookUrl = "https://hooks.example.com/rag";

        private readonly RagApiFactory _factory;

        public WebhookTests(RagApiFactory factory) => _factory = factory;

        public async Task InitializeAsync() => await SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient Login(int userId, int? spaceId = null)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private async Task<(int Id, string Secret)> CreateHookAsync(int userId = SeedData.UserA, string url = HookUrl, string[]? events = null)
        {
            var response = await Login(userId).PostAsJsonAsync("/api/v1/Webhook", new { name = "hook", url, events = events ?? new[] { "draft.created", "draft.accepted", "draft.rejected" } });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (body.GetProperty("id").GetInt32(), body.GetProperty("secret").GetString()!);
        }

        private async Task<int> CreateDraftAsync(string question = "q", string answer = "a", int userId = SeedData.UserA)
        {
            var response = await Login(userId, Space1).PostAsJsonAsync("/api/v1/Inbox", new { question, answer, sourceRef = "note#1" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }

        private async Task<int> RunDispatcherAsync()
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>().RunOnceAsync();
        }

        private Task<List<WebhookDelivery>> DeliveriesAsync() => _factory.WithDbAsync(db => db.WebhookDelivery.AsNoTracking().OrderBy(d => d.ID).ToListAsync());

        private Task MakeDueAsync() => _factory.WithDbAsync(async db =>
        {
            foreach (var d in await db.WebhookDelivery.Where(x => x.Status == DeliveryStatus.Pending).ToListAsync()) d.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        });

        [Fact]
        public async Task Secret_is_shown_once_and_stored_encrypted()
        {
            var (id, secret) = await CreateHookAsync();

            Assert.StartsWith("whsec_", secret);
            var stored = await _factory.WithDbAsync(db => db.Webhook.AsNoTracking().FirstAsync(w => w.ID == id));
            Assert.DoesNotContain(secret, stored.SecretProtected);
            using var scope = _factory.Services.CreateScope();
            Assert.Equal(secret, scope.ServiceProvider.GetRequiredService<IWebhookSecretProtector>().Unprotect(stored.SecretProtected));

            var list = await Login(SeedData.UserA).GetStringAsync("/api/v1/Webhook");
            Assert.DoesNotContain(secret, list);
            Assert.DoesNotContain("secret", list, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Unsafe_urls_unknown_events_and_the_per_user_cap_are_rejected()
        {
            var client = Login(SeedData.UserA);

            foreach (var url in new[] { "http://example.com/x", "https://localhost/x", "https://169.254.169.254/x", "https://10.0.0.1/x" })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Webhook", new { name = "x", url, events = new[] { "draft.created" } })).StatusCode);
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Webhook", new { name = "x", url = HookUrl, events = new[] { "nope" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Webhook", new { name = "x", url = HookUrl, events = Array.Empty<string>() })).StatusCode);

            for (var i = 0; i < WebhookService.MaxPerUser; i++) await CreateHookAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Webhook", new { name = "x", url = HookUrl, events = new[] { "draft.created" } })).StatusCode);
        }

        [Fact]
        public async Task Webhooks_are_private_and_never_manageable_with_an_api_token()
        {
            var (id, _) = await CreateHookAsync(SeedData.UserA);

            Assert.Equal("[]", await Login(SeedData.UserB).GetStringAsync("/api/v1/Webhook"));
            Assert.Equal(HttpStatusCode.NotFound, (await Login(SeedData.UserB).DeleteAsync($"/api/v1/Webhook/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Login(SeedData.UserB).PostAsync($"/api/v1/Webhook/{id}/test", null)).StatusCode);

            var tokenResponse = await Login(SeedData.UserA).PostAsJsonAsync("/api/v1/ApiToken", new { name = "t", scopes = new[] { "inbox.write", "knowledge.search" } });
            var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            var pat = _factory.CreateClient();
            pat.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await pat.GetAsync("/api/v1/Webhook")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await pat.PostAsJsonAsync("/api/v1/Webhook", new { name = "x", url = HookUrl, events = new[] { "draft.created" } })).StatusCode);
        }

        [Fact]
        public async Task Events_are_signed_and_carry_ids_but_not_the_answer_text()
        {
            var (_, secret) = await CreateHookAsync();

            await CreateDraftAsync("怎麼報帳？", "機密的答案本文");
            Assert.Equal(1, await RunDispatcherAsync());

            var request = Assert.Single(_factory.WebhookSender.Requests);
            Assert.Equal("draft.created", request.Event);
            Assert.Equal(HookUrl, request.Url.ToString());
            Assert.DoesNotContain("機密的答案本文", request.Body);
            Assert.DoesNotContain("怎麼報帳", request.Body);
            var payload = JsonDocument.Parse(request.Body).RootElement;
            Assert.Equal("draft.created", payload.GetProperty("event").GetString());
            Assert.Equal(Space1, payload.GetProperty("spaceID").GetInt32());
            Assert.Equal("note#1", payload.GetProperty("data").GetProperty("sourceRef").GetString());
            Assert.Equal(secret, request.Secret);
            // 對方驗證簽章：HMAC(secret, "<ts>.<body>")
            Assert.Equal(WebhookSignature.Compute(secret, request.Timestamp.ToUnixTimeSeconds(), request.Body), WebhookSignature.Compute(request.Secret, request.Timestamp.ToUnixTimeSeconds(), request.Body));

            var delivery = Assert.Single(await DeliveriesAsync());
            Assert.Equal(DeliveryStatus.Succeeded, delivery.Status);
            Assert.Equal(1, delivery.Attempts);
            Assert.Equal(delivery.EventID, payload.GetProperty("id").GetString());
        }

        [Fact]
        public async Task Accept_and_reject_publish_events_only_to_subscribers_of_the_owner()
        {
            await CreateHookAsync(SeedData.UserA, events: new[] { "draft.accepted" });
            await CreateHookAsync(SeedData.UserB, events: new[] { "draft.created", "draft.accepted" });

            var a = await CreateDraftAsync("a", "a");
            await RunDispatcherAsync();
            Assert.Empty(_factory.WebhookSender.Requests); // A 只訂閱 accepted；B 不是這筆草稿的主人

            var accept = await Login(SeedData.UserA, Space1).PostAsJsonAsync($"/api/v1/Inbox/{a}/accept", new { qaItemTypeID = SeedData.QaItemType });
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            await RunDispatcherAsync();
            var request = Assert.Single(_factory.WebhookSender.Requests);
            Assert.Equal("draft.accepted", request.Event);
            Assert.True(JsonDocument.Parse(request.Body).RootElement.GetProperty("data").GetProperty("questionID").GetInt32() > 0);
        }

        [Fact]
        public async Task Failed_deliveries_are_retried_with_backoff_and_then_marked_dead()
        {
            await CreateHookAsync();
            _factory.WebhookSender.Handler = _ => new WebhookResult(500, "對方回應 HTTP 500");
            await CreateDraftAsync();

            var expectedDelays = WebhookDispatcher.RetryDelays;
            for (var attempt = 1; attempt < WebhookDispatcher.MaxAttempts; attempt++)
            {
                Assert.Equal(1, await RunDispatcherAsync());
                var d = Assert.Single(await DeliveriesAsync());
                Assert.Equal(DeliveryStatus.Pending, d.Status);
                Assert.Equal(attempt, d.Attempts);
                Assert.Equal(500, d.LastStatusCode);
                var wait = d.NextAttemptAt!.Value - DateTime.UtcNow;
                Assert.InRange(wait, expectedDelays[attempt - 1] - TimeSpan.FromSeconds(30), expectedDelays[attempt - 1] + TimeSpan.FromSeconds(5));

                Assert.Equal(0, await RunDispatcherAsync()); // 還沒到時間：不會再送
                await MakeDueAsync();
            }

            Assert.Equal(1, await RunDispatcherAsync());
            var dead = Assert.Single(await DeliveriesAsync());
            Assert.Equal(DeliveryStatus.Dead, dead.Status);
            Assert.Equal(WebhookDispatcher.MaxAttempts, dead.Attempts);
            Assert.Null(dead.NextAttemptAt);
            Assert.Equal(0, await RunDispatcherAsync()); // Dead 不再自動重試
        }

        [Fact]
        public async Task A_success_resets_the_failure_streak_and_a_throwing_sender_is_just_a_failed_attempt()
        {
            var (hookId, _) = await CreateHookAsync();
            await _factory.WithDbAsync(async db => { (await db.Webhook.FirstAsync()).ConsecutiveFailures = 3; await db.SaveChangesAsync(); });

            _factory.WebhookSender.Handler = _ => throw new InvalidOperationException("boom");
            await CreateDraftAsync("x", "x");
            await RunDispatcherAsync();
            var failed = Assert.Single(await DeliveriesAsync());
            Assert.Equal(DeliveryStatus.Pending, failed.Status);
            Assert.NotNull(failed.LastError);

            _factory.WebhookSender.Handler = _ => new WebhookResult(204, null);
            await MakeDueAsync();
            await RunDispatcherAsync();

            Assert.Equal(DeliveryStatus.Succeeded, (await DeliveriesAsync()).Single().Status);
            Assert.Equal(0, (await _factory.WithDbAsync(db => db.Webhook.AsNoTracking().FirstAsync(w => w.ID == hookId))).ConsecutiveFailures);
        }

        [Fact]
        public async Task Blocked_targets_are_dead_immediately_and_repeated_death_disables_the_webhook()
        {
            var (hookId, _) = await CreateHookAsync();
            _factory.WebhookSender.Handler = _ => new WebhookResult(null, "這個網域指向內部或保留的位址，已拒絕連線", Permanent: true);

            for (var i = 0; i < WebhookDispatcher.DisableAfterConsecutiveDead; i++)
            {
                await CreateDraftAsync($"q{i}", $"a{i}");
                await RunDispatcherAsync();
            }

            Assert.All(await DeliveriesAsync(), d => { Assert.Equal(DeliveryStatus.Dead, d.Status); Assert.Equal(1, d.Attempts); });
            var hook = await _factory.WithDbAsync(db => db.Webhook.AsNoTracking().FirstAsync(w => w.ID == hookId));
            Assert.False(hook.IsActive);
            Assert.Contains("自動停用", hook.DisabledReason);

            // 停用後不再送，也不會累積
            await CreateDraftAsync("after", "after");
            Assert.Empty(await _factory.WithDbAsync(db => db.WebhookDelivery.Where(d => d.Webhook.IsActive).ToListAsync()));
        }

        [Fact]
        public async Task Dead_deliveries_can_be_redelivered_by_hand_unless_the_webhook_is_disabled()
        {
            var (hookId, _) = await CreateHookAsync();
            _factory.WebhookSender.Handler = _ => new WebhookResult(null, "x", Permanent: true);
            await CreateDraftAsync();
            await RunDispatcherAsync();
            var dead = Assert.Single(await DeliveriesAsync());
            Assert.Equal(DeliveryStatus.Dead, dead.Status);

            _factory.WebhookSender.Handler = _ => new WebhookResult(200, null);
            var response = await Login(SeedData.UserA).PostAsync($"/api/v1/Webhook/{hookId}/deliveries/{dead.ID}/redeliver", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var vm = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Succeeded", vm.GetProperty("status").GetString());
            Assert.Equal(1, vm.GetProperty("attempts").GetInt32());
            // 重送的是同一個事件（對方用 id 去重）
            Assert.Equal(dead.EventID, JsonDocument.Parse(_factory.WebhookSender.Requests.Last().Body).RootElement.GetProperty("id").GetString());

            var other = await Login(SeedData.UserB).PostAsync($"/api/v1/Webhook/{hookId}/deliveries/{dead.ID}/redeliver", null);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await Login(SeedData.UserA).PutAsJsonAsync($"/api/v1/Webhook/{hookId}", new { name = "hook", url = HookUrl, events = new[] { "draft.created" }, isActive = false })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Login(SeedData.UserA).PostAsync($"/api/v1/Webhook/{hookId}/deliveries/{dead.ID}/redeliver", null)).StatusCode);
        }

        [Fact]
        public async Task Re_enabling_clears_the_failure_state_and_disabling_kills_queued_events()
        {
            var (hookId, _) = await CreateHookAsync();
            _factory.WebhookSender.Handler = _ => new WebhookResult(503, "對方回應 HTTP 503");
            await CreateDraftAsync();
            await RunDispatcherAsync();
            Assert.Equal(DeliveryStatus.Pending, (await DeliveriesAsync()).Single().Status);

            var off = await Login(SeedData.UserA).PutAsJsonAsync($"/api/v1/Webhook/{hookId}", new { name = "hook", url = HookUrl, events = new[] { "draft.created" }, isActive = false });
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.Equal(DeliveryStatus.Dead, (await DeliveriesAsync()).Single().Status); // 排隊中的事件不會在重新啟用後被一次補送

            await _factory.WithDbAsync(async db => { (await db.Webhook.FirstAsync()).ConsecutiveFailures = 4; await db.SaveChangesAsync(); });
            var on = await Login(SeedData.UserA).PutAsJsonAsync($"/api/v1/Webhook/{hookId}", new { name = "hook", url = HookUrl, events = new[] { "draft.created" }, isActive = true });
            var vm = await on.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(vm.GetProperty("isActive").GetBoolean());
            Assert.Equal(0, vm.GetProperty("consecutiveFailures").GetInt32());
            Assert.Equal(JsonValueKind.Null, vm.GetProperty("disabledReason").ValueKind);
        }

        [Fact]
        public async Task Test_endpoint_sends_a_ping_and_reports_the_outcome()
        {
            var (hookId, _) = await CreateHookAsync(events: new[] { "draft.created" });

            var ok = await Login(SeedData.UserA).PostAsync($"/api/v1/Webhook/{hookId}/test", null);
            var okBody = await ok.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Succeeded", okBody.GetProperty("status").GetString());
            Assert.Equal("ping", _factory.WebhookSender.Requests.Single().Event);

            _factory.WebhookSender.Handler = _ => new WebhookResult(500, "對方回應 HTTP 500");
            var bad = await (await Login(SeedData.UserA).PostAsync($"/api/v1/Webhook/{hookId}/test", null)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Pending", bad.GetProperty("status").GetString());
            Assert.Equal("對方回應 HTTP 500", bad.GetProperty("lastError").GetString());

            var list = await Login(SeedData.UserA).GetFromJsonAsync<JsonElement>($"/api/v1/Webhook/{hookId}/deliveries");
            Assert.Equal(2, list.GetArrayLength());
        }
    }
}
