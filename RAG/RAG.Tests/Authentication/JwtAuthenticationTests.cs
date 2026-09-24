using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace RAG.Tests.Authentication
{
    /// <summary>
    /// 經由真實 ASP.NET Core middleware / authorization pipeline 驗證 JWT Bearer 行為
    /// </summary>
    public class JwtAuthenticationTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;

        public JwtAuthenticationTests(RagApiFactory factory)
        {
            _factory = factory;
        }

        public Task InitializeAsync() => SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        public static TheoryData<string, string> ProtectedEndpoints => new()
        {
            { "GET", "/api/Chat" },
            { "POST", "/api/Chat" },
            { "POST", "/api/Chat/ChatStream" },
            { "GET", $"/api/Chat/{SeedData.ChatA}" },
            { "DELETE", $"/api/Chat/{SeedData.ChatA}" },
            { "GET", "/api/Question?Type=0" },
            { "POST", "/api/Question" },
            { "PUT", "/api/Question" },
            { "DELETE", $"/api/Question/{SeedData.QuestionA}" },
            { "GET", $"/api/User/{SeedData.UserA}" },
            { "POST", "/api/User" },
            { "PUT", "/api/User" },
            { "DELETE", $"/api/User/{SeedData.UserA}" },
            { "GET", "/api/QaType" },
            { "POST", "/api/QaType" },
            { "PUT", "/api/QaType" },
            { "DELETE", "/api/QaType/1" },
            { "GET", "/api/QaTypeItem" },
            { "POST", "/api/QaTypeItem" },
            { "PUT", "/api/QaTypeItem" },
            { "DELETE", "/api/QaTypeItem/1" },
        };

        [Theory]
        [MemberData(nameof(ProtectedEndpoints))]
        public async Task Missing_token_returns_401_on_every_protected_endpoint(string method, string url)
        {
            var client = _factory.CreateClientWithToken(null);

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = method is "POST" or "PUT" ? new StringContent("{}", Encoding.UTF8, "application/json") : null
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        }

        public static TheoryData<string, string> InvalidTokens()
        {
            var otherKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            return new TheoryData<string, string>
            {
                { "malformed", "not-a-jwt" },
                { "malformed-segments", "abc.def.ghi" },
                { "unsigned-alg-none", TestTokens.CreateUnsigned() },
                { "wrong-signature", TestTokens.Create(otherKey) },
            };
        }

        [Theory]
        [MemberData(nameof(InvalidTokens))]
        public async Task Malformed_or_badly_signed_token_returns_401(string _, string token)
        {
            var response = await _factory.CreateClientWithToken(token).GetAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Wrong_issuer_returns_401()
        {
            var token = TestTokens.Create(_factory.SecretKey, issuer: "Attacker");

            var response = await _factory.CreateClientWithToken(token).GetAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Wrong_audience_returns_401()
        {
            var token = TestTokens.Create(_factory.SecretKey, audience: "OtherAudience");

            var response = await _factory.CreateClientWithToken(token).GetAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Expired_token_returns_401()
        {
            var now = DateTime.UtcNow;
            var token = TestTokens.Create(_factory.SecretKey, notBefore: now.AddHours(-2), expires: now.AddSeconds(-5));

            var response = await _factory.CreateClientWithToken(token).GetAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-1")]
        public async Task Token_without_valid_numeric_subject_returns_401(string? subject)
        {
            var token = TestTokens.Create(_factory.SecretKey, subject: subject);

            var response = await _factory.CreateClientWithToken(token).GetAsync("/api/Chat");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("refresh")]
        [InlineData(null)]
        public async Task Non_access_token_returns_401(string? tokenUse)
        {
            var token = TestTokens.Create(_factory.SecretKey, tokenUse: tokenUse);

            var response = await _factory.CreateClientWithToken(token).GetAsync("/api/Chat");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Invalid_token_does_not_execute_mutation()
        {
            var client = _factory.CreateClientWithToken("not-a-jwt");

            var response = await client.DeleteAsync($"/api/Chat/{SeedData.ChatA}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(await SeedData.ChatExistsAsync(_factory, SeedData.ChatA));
        }

        [Fact]
        public async Task Valid_token_reaches_protected_endpoint()
        {
            var token = TestTokens.Create(_factory.SecretKey, subject: SeedData.UserA.ToString());

            var response = await _factory.CreateClientWithToken(token).GetAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.Equal(SeedData.UserAAccount, body!["account"].ToString());
        }

        [Fact]
        public async Task Login_is_anonymous_and_issues_usable_access_token_but_not_usable_refresh_token()
        {
            var anonymous = _factory.CreateClientWithToken(null);

            var login = await anonymous.PostAsJsonAsync("/api/Auth/Login", new { Account = SeedData.UserAAccount, Password = SeedData.UserAPassword });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var body = await login.Content.ReadFromJsonAsync<LoginBody>();
            Assert.Equal(SeedData.UserA, body!.UserID);
            Assert.False(string.IsNullOrEmpty(body.AccessToken));

            var withAccess = await _factory.CreateClientWithToken(body.AccessToken).GetAsync($"/api/User/{SeedData.UserA}");
            Assert.Equal(HttpStatusCode.OK, withAccess.StatusCode);

            var withRefresh = await _factory.CreateClientWithToken(body.RefreshToken).GetAsync($"/api/User/{SeedData.UserA}");
            Assert.Equal(HttpStatusCode.Unauthorized, withRefresh.StatusCode);
        }

        [Fact]
        public async Task Login_with_wrong_password_returns_401()
        {
            var response = await _factory.CreateClientWithToken(null)
                .PostAsJsonAsync("/api/Auth/Login", new { Account = SeedData.UserAAccount, Password = "wrong" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private sealed record LoginBody(int UserID, string AccessToken, string RefreshToken);
    }
}
