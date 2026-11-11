using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Spaces;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RAG.Tests.Platform
{
    /// <summary>
    /// 使用者自己建立空間：建立者是 owner，空間之間的知識與對話互相隔離
    /// </summary>
    public class SpaceManagementTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;

        public SpaceManagementTests(RagApiFactory factory) => _factory = factory;

        public Task InitializeAsync() => SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient Login(int userId, int? spaceId = null)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private async Task<JsonElement> CreateAsync(int userId, string name)
        {
            var response = await Login(userId).PostAsJsonAsync("/api/v1/Space", new { name });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        [Fact]
        public async Task Creating_a_space_makes_the_creator_its_owner_without_changing_the_default()
        {
            var created = await CreateAsync(SeedData.UserA, "  研究筆記  ");

            Assert.Equal("研究筆記", created.GetProperty("name").GetString());
            Assert.Equal("owner", created.GetProperty("role").GetString());
            Assert.False(created.GetProperty("isDefault").GetBoolean());
            Assert.StartsWith("t-", created.GetProperty("key").GetString());

            var spaces = await Login(SeedData.UserA).GetFromJsonAsync<JsonElement>("/api/v1/Space");
            Assert.Equal(2, spaces.GetArrayLength());
            Assert.Equal(SeedData.DefaultSpace, spaces.EnumerateArray().Single(s => s.GetProperty("isDefault").GetBoolean()).GetProperty("spaceID").GetInt32());
        }

        [Fact]
        public async Task The_new_space_is_usable_and_isolated_from_other_spaces_and_users()
        {
            var id = (await CreateAsync(SeedData.UserA, "團隊")).GetProperty("spaceID").GetInt32();

            // owner 可以在新空間寫入知識
            var write = await Login(SeedData.UserA, id).PostAsJsonAsync("/api/v1/Question", new { qaItemTypeID = SeedData.QaItemType, question = "新空間的問題", answer = "新空間的答案" });
            Assert.Equal(HttpStatusCode.OK, write.StatusCode);
            Assert.Single(_factory.RagGateway.Calls, c => c.Operation == "upsert" && c.SpaceId == id);

            // 內容只在這個空間；原本的空間看不到
            Assert.DoesNotContain("新空間的問題", await Login(SeedData.UserA, SeedData.DefaultSpace).GetStringAsync("/api/v1/Question?Type=0"));
            Assert.Contains("新空間的問題", await Login(SeedData.UserA, id).GetStringAsync("/api/v1/Question?Type=0"));

            // 別的使用者不是成員：帶這個空間編號會被拒絕，也看不到它
            Assert.Equal(HttpStatusCode.Forbidden, (await Login(SeedData.UserB, id).GetAsync("/api/v1/Question?Type=0")).StatusCode);
            Assert.DoesNotContain("團隊", await Login(SeedData.UserB).GetStringAsync("/api/v1/Space"));
        }

        [Fact]
        public async Task Names_are_validated()
        {
            var client = Login(SeedData.UserA);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Space", new { name = "" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Space", new { name = "   " })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Space", new { name = new string('a', 101) })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/Space", new { name = new string('a', 100) })).StatusCode);
        }

        [Fact]
        public async Task Each_user_can_own_a_limited_number_of_spaces()
        {
            await _factory.WithDbAsync(async db =>
            {
                for (var i = 0; i < SpaceManagementService.MaxOwnedSpaces; i++)
                {
                    var space = new Space { Key = $"seed-{i}", Name = $"s{i}", IsActive = true, CreateAt = DateTime.UtcNow };
                    space.AccountSpace.Add(new AccountSpace { UserID = SeedData.UserA, Role = "owner", IsActive = true, IsDefault = false, CreateAt = DateTime.UtcNow });
                    db.Space.Add(space);
                }
                await db.SaveChangesAsync();
            });

            Assert.Equal(HttpStatusCode.BadRequest, (await Login(SeedData.UserA).PostAsJsonAsync("/api/v1/Space", new { name = "超過上限" })).StatusCode);
            // 別人的額度不受影響
            Assert.Equal(HttpStatusCode.OK, (await Login(SeedData.UserB).PostAsJsonAsync("/api/v1/Space", new { name = "B 的空間" })).StatusCode);
        }

        [Fact]
        public async Task Only_a_login_session_can_create_or_rename_spaces_never_an_api_token()
        {
            var tokenResponse = await Login(SeedData.UserA).PostAsJsonAsync("/api/v1/ApiToken", new { name = "t", scopes = new[] { "space.read", "knowledge.search", "inbox.write" } });
            var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            var pat = _factory.CreateClient();
            pat.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Assert.Equal(HttpStatusCode.Unauthorized, (await pat.PostAsJsonAsync("/api/v1/Space", new { name = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await pat.PutAsJsonAsync($"/api/v1/Space/{SeedData.DefaultSpace}", new { name = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/v1/Space", new { name = "x" })).StatusCode);
        }

        [Fact]
        public async Task Only_the_owner_can_rename_and_other_peoples_spaces_look_nonexistent()
        {
            var mine = (await CreateAsync(SeedData.UserA, "舊名")).GetProperty("spaceID").GetInt32();

            var renamed = await Login(SeedData.UserA).PutAsJsonAsync($"/api/v1/Space/{mine}", new { name = "新名" });
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            Assert.Equal("新名", (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());

            // 不是成員：不知道這個空間存在
            Assert.Equal(HttpStatusCode.NotFound, (await Login(SeedData.UserB).PutAsJsonAsync($"/api/v1/Space/{mine}", new { name = "被改" })).StatusCode);

            // 是成員但不是 owner：不能改
            await _factory.WithDbAsync(async db =>
            {
                db.AccountSpace.Add(new AccountSpace { UserID = SeedData.UserB, SpaceID = mine, Role = "member", IsActive = true, IsDefault = false, CreateAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.Forbidden, (await Login(SeedData.UserB).PutAsJsonAsync($"/api/v1/Space/{mine}", new { name = "被改" })).StatusCode);

            Assert.Equal("新名", (await _factory.WithDbAsync(db => db.Space.AsNoTracking().FirstAsync(s => s.ID == mine))).Name);
        }

        [Fact]
        public async Task Creating_spaces_is_rate_limited_per_user()
        {
            using var limited = _factory.WithWebHostBuilder(b => b.UseSetting("SpaceManagement:CreatePerMinute", "2"));
            await SeedData.ResetAsync(_factory);
            var client = limited.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(_factory.SecretKey, subject: SeedData.UserA.ToString()));

            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/Space", new { name = "1" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/Space", new { name = "2" })).StatusCode);
            Assert.Equal((HttpStatusCode)429, (await client.PostAsJsonAsync("/api/v1/Space", new { name = "3" })).StatusCode);
        }

        [Fact]
        public async Task Spaces_created_this_way_show_up_in_all_scope_search()
        {
            var id = (await CreateAsync(SeedData.UserA, "跨空間")).GetProperty("spaceID").GetInt32();

            await Login(SeedData.UserA, SeedData.DefaultSpace).PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "all" });

            Assert.Equal(new[] { SeedData.DefaultSpace, id }.Order(), _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);
        }
    }
}
