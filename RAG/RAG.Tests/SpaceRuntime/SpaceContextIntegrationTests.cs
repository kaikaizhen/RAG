using RAG.Models.Entities;
using RAG.SpaceRuntime;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.SpaceRuntime
{
    /// <summary>
    /// 經由真實 authentication → authorization → SpaceContext middleware pipeline 驗證 Space 解析
    /// </summary>
    public class SpaceContextIntegrationTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        // 使用者：A(1) 多 membership 且有預設；B(2) 無 membership；C(3) 單一 membership；D(4) 多 membership 無預設
        private const int UserC = 3;
        private const int UserD = 4;

        // Space
        private const int SpaceA_Default = 100;
        private const int SpaceA_Viewer = 101;
        private const int SpaceNotMember = 102;
        private const int SpaceInactive = 103;
        private const int SpaceMembershipInactive = 104;
        private const int SpaceDefaultKey = 105;
        private const int SpaceUnknownRole = 106;

        private const string CurrentUrl = "/api/v1/Space/current";

        private readonly RagApiFactory _factory;

        public SpaceContextIntegrationTests(RagApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory, withDefaultSpace: false);
            await _factory.WithDbAsync(async db =>
            {
                db.UserCache.AddRange(
                    new UserCache { ID = UserC, Account = "userC", Password = "passC", IsActive = true, Manage = "free" },
                    new UserCache { ID = UserD, Account = "userD", Password = "passD", IsActive = true, Manage = "free" });

                db.Space.AddRange(
                    new Space { ID = SpaceA_Default, Key = "team-a", Name = "Team A", IsActive = true },
                    new Space { ID = SpaceA_Viewer, Key = "team-b", Name = "Team B", IsActive = true },
                    new Space { ID = SpaceNotMember, Key = "team-c", Name = "Team C", IsActive = true },
                    new Space { ID = SpaceInactive, Key = "archived", Name = "Archived", IsActive = false },
                    new Space { ID = SpaceMembershipInactive, Key = "team-d", Name = "Team D", IsActive = true },
                    new Space { ID = SpaceDefaultKey, Key = "default", Name = "Default", IsActive = true },
                    new Space { ID = SpaceUnknownRole, Key = "team-e", Name = "Team E", IsActive = true });

                db.AccountSpace.AddRange(
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = SpaceA_Default, Role = SpaceRoles.Member, IsActive = true, IsDefault = true },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = SpaceA_Viewer, Role = SpaceRoles.Viewer, IsActive = true },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = SpaceInactive, Role = SpaceRoles.Owner, IsActive = true },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = SpaceMembershipInactive, Role = SpaceRoles.Owner, IsActive = false },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = SpaceUnknownRole, Role = "superadmin", IsActive = true },
                    new AccountSpace { UserID = UserC, SpaceID = SpaceA_Viewer, Role = SpaceRoles.Owner, IsActive = true },
                    new AccountSpace { UserID = UserD, SpaceID = SpaceA_Default, Role = SpaceRoles.Member, IsActive = true },
                    new AccountSpace { UserID = UserD, SpaceID = SpaceA_Viewer, Role = SpaceRoles.Member, IsActive = true });

                await db.SaveChangesAsync();
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient ClientFor(int userId) =>
            _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));

        private static HttpRequestMessage Get(string url, params (string Name, string Value)[] headers)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
            return request;
        }

        private static async Task<CurrentSpace> ReadCurrent(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CurrentSpace>())!;
        }

        // ---------- 401：未登入 / 無效 token 不建立 SpaceContext，也不 fallback ----------

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-jwt")]
        public async Task Unauthenticated_returns_401_even_with_space_hint(string? token)
        {
            var response = await _factory.CreateClientWithToken(token)
                .SendAsync(Get(CurrentUrl, ("X-Space-Id", SpaceA_Default.ToString())));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Space_list_requires_authentication()
        {
            var response = await _factory.CreateClientWithToken(null).GetAsync("/api/v1/Space");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---------- 合法 membership ----------

        [Fact]
        public async Task No_hint_uses_account_default_space()
        {
            var current = await ReadCurrent(await ClientFor(SeedData.UserA).SendAsync(Get(CurrentUrl)));

            Assert.Equal(SpaceA_Default, current.SpaceID);
            Assert.Equal(SpaceRoles.Member, current.Role);
            Assert.Null(current.RepositoryID);
            Assert.Null(current.ProjectID);
        }

        [Fact]
        public async Task Same_account_can_switch_between_membership_spaces()
        {
            var client = ClientFor(SeedData.UserA);

            var first = await ReadCurrent(await client.SendAsync(Get(CurrentUrl, ("X-Space-Id", SpaceA_Default.ToString()))));
            var second = await ReadCurrent(await client.SendAsync(Get(CurrentUrl, ("X-Space-Id", SpaceA_Viewer.ToString()))));

            Assert.Equal(SpaceA_Default, first.SpaceID);
            Assert.Equal(SpaceA_Viewer, second.SpaceID);
            Assert.Equal(SpaceRoles.Viewer, second.Role);
            Assert.DoesNotContain(SpacePermissions.KnowledgeWrite, second.Permissions);
        }

        [Fact]
        public async Task Single_membership_is_used_without_hint()
        {
            var current = await ReadCurrent(await ClientFor(UserC).SendAsync(Get(CurrentUrl)));

            Assert.Equal(SpaceA_Viewer, current.SpaceID);
            Assert.Equal(SpaceRoles.Owner, current.Role);
        }

        [Fact]
        public async Task Space_list_contains_only_valid_memberships()
        {
            var response = await ClientFor(SeedData.UserA).GetAsync("/api/v1/Space");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var spaces = await response.Content.ReadFromJsonAsync<List<SpaceItem>>();
            Assert.Equal(new[] { SpaceA_Default, SpaceA_Viewer }, spaces!.Select(s => s.SpaceID));
        }

        // ---------- 403：未授權 selection，永不降級 ----------

        [Theory]
        [InlineData(SpaceNotMember)]
        [InlineData(SpaceInactive)]
        [InlineData(SpaceMembershipInactive)]
        [InlineData(SpaceUnknownRole)]
        [InlineData(999)]
        public async Task Unauthorized_space_selection_returns_403(int spaceId)
        {
            var response = await ClientFor(SeedData.UserA).SendAsync(Get(CurrentUrl, ("X-Space-Id", spaceId.ToString())));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task No_mapping_returns_403_and_never_falls_back_to_default_space()
        {
            var client = ClientFor(SeedData.UserB);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Get(CurrentUrl))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Get(CurrentUrl, ("X-Space-Id", SpaceDefaultKey.ToString())))).StatusCode);

            var spaces = await client.GetFromJsonAsync<List<SpaceItem>>("/api/v1/Space");
            Assert.Empty(spaces!);
        }

        [Fact]
        public async Task Multiple_memberships_without_default_require_selection()
        {
            var client = ClientFor(UserD);

            var noHint = await client.SendAsync(Get(CurrentUrl));
            Assert.Equal(HttpStatusCode.Forbidden, noHint.StatusCode);
            Assert.Contains("selection required", await noHint.Content.ReadAsStringAsync());

            var withHint = await ReadCurrent(await client.SendAsync(Get(CurrentUrl, ("X-Space-Id", SpaceA_Viewer.ToString()))));
            Assert.Equal(SpaceA_Viewer, withHint.SpaceID);
        }

        [Fact]
        public async Task Repository_or_project_selection_is_rejected_without_authority_source()
        {
            var client = ClientFor(SeedData.UserA);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Get(CurrentUrl, ("X-Repository-Id", "repo-1")))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Get(CurrentUrl, ("X-Project-Id", "project-1")))).StatusCode);
        }

        // ---------- 400：hint 格式錯誤 ----------

        [Theory]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData(" 100")]
        public async Task Malformed_space_hint_returns_400(string hint)
        {
            var response = await ClientFor(SeedData.UserA).SendAsync(Get(CurrentUrl, ("X-Space-Id", hint)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Multiple_space_hints_return_400()
        {
            var request = Get(CurrentUrl);
            request.Headers.TryAddWithoutValidation("X-Space-Id", new[] { SpaceA_Default.ToString(), SpaceA_Viewer.ToString() });

            var response = await ClientFor(SeedData.UserA).SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---------- isolation：body / query 不可改變已解析 context ----------

        [Fact]
        public async Task Query_space_id_cannot_override_resolved_context()
        {
            var client = ClientFor(SeedData.UserA);

            var withHeader = await ReadCurrent(await client.SendAsync(
                Get($"{CurrentUrl}?spaceId={SpaceNotMember}&space_id={SpaceNotMember}", ("X-Space-Id", SpaceA_Viewer.ToString()))));
            Assert.Equal(SpaceA_Viewer, withHeader.SpaceID);

            var withoutHeader = await ReadCurrent(await client.SendAsync(Get($"{CurrentUrl}?space_id={SpaceNotMember}")));
            Assert.Equal(SpaceA_Default, withoutHeader.SpaceID);
        }

        [Fact]
        public async Task Body_space_id_cannot_override_resolved_context()
        {
            var request = Get(CurrentUrl, ("X-Space-Id", SpaceA_Viewer.ToString()));
            request.Content = JsonContent.Create(new { space_id = SpaceNotMember, spaceId = SpaceNotMember });

            var current = await ReadCurrent(await ClientFor(SeedData.UserA).SendAsync(request));

            Assert.Equal(SpaceA_Viewer, current.SpaceID);
        }

        [Fact]
        public async Task Space_scoped_endpoints_require_membership_but_account_endpoints_do_not()
        {
            var client = ClientFor(SeedData.UserB);

            // 帳號層級 API 不需要 SpaceContext
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/User/{SeedData.UserB}")).StatusCode);

            // Phase 3：Chat / Question 需要 SpaceContext，無 membership 一律 403
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/Chat")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/Question?Type=0")).StatusCode);
        }

        private sealed record CurrentSpace(int SpaceID, string Key, string Name, string Role, List<string> Permissions, string? RepositoryID, string? ProjectID);

        private sealed record SpaceItem(int SpaceID, string Key, string Name, string Role, bool IsDefault);
    }
}
