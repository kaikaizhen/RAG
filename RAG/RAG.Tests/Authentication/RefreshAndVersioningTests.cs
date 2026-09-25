using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.Authentication
{
    public class RefreshAndVersioningTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const string RefreshUrl = "/api/v1/Auth/Refresh";
        private readonly RagApiFactory _factory;

        public RefreshAndVersioningTests(RagApiFactory factory) => _factory = factory;

        public Task InitializeAsync() => SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<Session> LoginAsync()
        {
            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync("/api/v1/Auth/Login", new { Account = SeedData.UserAAccount, Password = SeedData.UserAPassword });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Session>())!;
        }

        // ---------- Refresh ----------

        [Fact]
        public async Task Refresh_is_anonymous_and_issues_a_new_usable_session()
        {
            var session = await LoginAsync();

            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync(RefreshUrl, new { session.RefreshToken });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var refreshed = (await response.Content.ReadFromJsonAsync<Session>())!;
            Assert.Equal(SeedData.UserA, refreshed.UserID);
            Assert.NotEqual(session.AccessToken, refreshed.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClientWithToken(refreshed.AccessToken).GetAsync($"/api/v1/User/{SeedData.UserA}")).StatusCode);
        }

        [Fact]
        public async Task Access_token_cannot_be_used_as_refresh_token()
        {
            var session = await LoginAsync();

            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync(RefreshUrl, new { RefreshToken = session.AccessToken });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        public static TheoryData<string> InvalidRefreshTokens()
        {
            var other = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var now = DateTime.UtcNow;
            return new TheoryData<string>
            {
                "not-a-jwt",
                TestTokens.CreateUnsigned(),
                TestTokens.Create(other, tokenUse: "refresh"),
            };
        }

        [Theory]
        [MemberData(nameof(InvalidRefreshTokens))]
        public async Task Forged_or_malformed_refresh_token_is_401(string token)
        {
            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync(RefreshUrl, new { RefreshToken = token });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Expired_refresh_token_is_401()
        {
            var now = DateTime.UtcNow;
            var token = TestTokens.Create(_factory.SecretKey, subject: SeedData.UserA.ToString(), tokenUse: "refresh", notBefore: now.AddHours(-9), expires: now.AddSeconds(-1));

            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync(RefreshUrl, new { RefreshToken = token });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Refresh_for_deleted_account_is_401()
        {
            var token = TestTokens.Create(_factory.SecretKey, subject: "999", tokenUse: "refresh");

            var response = await _factory.CreateClientWithToken(null).PostAsJsonAsync(RefreshUrl, new { RefreshToken = token });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---------- 版本化路由 ----------

        [Theory]
        [InlineData("/api/Chat")]
        [InlineData("/api/Space/current")]
        [InlineData("/api/Auth/Login")]
        public async Task Unversioned_routes_no_longer_exist(string url)
        {
            var token = TestTokens.Create(_factory.SecretKey, subject: SeedData.UserA.ToString());

            var response = await _factory.CreateClientWithToken(token).GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Unsupported_api_version_is_rejected()
        {
            var token = TestTokens.Create(_factory.SecretKey, subject: SeedData.UserA.ToString());

            var response = await _factory.CreateClientWithToken(token).GetAsync("/api/v2/Space");

            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, response.StatusCode.ToString());
        }

        private sealed record Session(int UserID, string AccessToken, string RefreshToken);
    }
}
