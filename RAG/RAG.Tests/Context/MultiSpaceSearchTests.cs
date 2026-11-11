using Microsoft.EntityFrameworkCore;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RAG.Tests.Context
{
    /// <summary>
    /// 檢索範圍：預設只找目前的 Space；scope=all 涵蓋「我」所有可讀的 Space，範圍由伺服器依 membership 決定
    /// </summary>
    public class MultiSpaceSearchTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const int Space1 = SeedData.DefaultSpace; // A: member
        private const int Space2 = 2;                     // A: viewer
        private const int Space3 = 3;                     // A: 沒有 membership
        private const int Space4 = 4;                     // A: membership 停用
        private const int Space5 = 5;                     // A: Space 本身停用

        private readonly RagApiFactory _factory;

        public MultiSpaceSearchTests(RagApiFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.Space.AddRange(
                    new Space { ID = Space2, Key = "team-2", Name = "Team 2", IsActive = true },
                    new Space { ID = Space3, Key = "team-3", Name = "Team 3", IsActive = true },
                    new Space { ID = Space4, Key = "team-4", Name = "Team 4", IsActive = true },
                    new Space { ID = Space5, Key = "team-5", Name = "Team 5", IsActive = false });
                db.AccountSpace.AddRange(
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = Space2, Role = "viewer", IsActive = true, IsDefault = false },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = Space4, Role = "member", IsActive = false, IsDefault = false },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = Space5, Role = "member", IsActive = true, IsDefault = false });
                db.Questions.AddRange(
                    Q(41, Space1, "space1-q", "space1-answer"),
                    Q(42, Space2, "space2-q", "space2-answer"),
                    Q(43, Space3, "space3-q", "space3-secret"),
                    Q(44, Space4, "space4-q", "space4-secret"),
                    Q(45, Space5, "space5-q", "space5-secret"));
                await db.SaveChangesAsync();
            });
            foreach (var (space, id, score) in new[] { (Space1, 41, 0.90), (Space2, 42, 0.88), (Space3, 43, 0.99), (Space4, 44, 0.98), (Space5, 45, 0.97) })
            {
                _factory.RagGateway.SearchHits[space] = new List<RagSearchHit> { new() { Id = $"v{id}", ReferenceId = id.ToString(), Question = "x", Answer = "ignored", Score = score } };
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static Questions Q(int id, int spaceId, string question, string answer) =>
            new() { ID = id, QaItemTypeID = SeedData.QaItemType, UserID = SeedData.UserB, SpaceID = spaceId, Status = KnowledgeStatus.Verified, Question = question, Answer = answer, VectorID = $"v{id}" };

        private HttpClient Login(int userId = SeedData.UserA, int? spaceId = Space1)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        private static int[] Ids(JsonElement array, string property) => array.EnumerateArray().Select(e => e.GetProperty(property).GetInt32()).ToArray();

        [Fact]
        public async Task Default_scope_searches_only_the_current_space()
        {
            var result = await JsonAsync(await Login().PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x" }));

            Assert.Equal("current", result.GetProperty("scope").GetString());
            Assert.Equal(new[] { Space1 }, result.GetProperty("searchedSpaceIDs").EnumerateArray().Select(e => e.GetInt32()).ToArray());
            Assert.Equal(new[] { 41 }, Ids(result.GetProperty("evidence"), "questionID"));
            Assert.Equal(new[] { Space1 }, _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);
        }

        [Fact]
        public async Task All_scope_covers_every_readable_space_and_labels_where_each_item_came_from()
        {
            var result = await JsonAsync(await Login().PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "all" }));

            Assert.Equal("all", result.GetProperty("scope").GetString());
            Assert.Equal(new[] { Space1, Space2 }, _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);
            Assert.Equal(new[] { 41, 42 }, Ids(result.GetProperty("evidence"), "questionID"));
            Assert.Equal(new[] { Space1, Space2 }, Ids(result.GetProperty("evidence"), "spaceID"));
            Assert.Equal(new[] { "default", "team-2" }, result.GetProperty("evidence").EnumerateArray().Select(e => e.GetProperty("spaceKey").GetString()).ToArray());
        }

        [Fact]
        public async Task Spaces_without_membership_or_with_inactive_membership_or_space_are_never_searched()
        {
            // 即使 RAG Server 把這些 Space 的命中混進來，也不能出現在結果裡
            _factory.RagGateway.LeakedSpaceIds.AddRange(new[] { Space3, Space4, Space5 });

            var raw = await (await Login().PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "all" })).Content.ReadAsStringAsync();

            Assert.DoesNotContain("space3-secret", raw);
            Assert.DoesNotContain("space4-secret", raw);
            Assert.DoesNotContain("space5-secret", raw);
            var searched = _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds!;
            Assert.DoesNotContain(Space3, searched);
            Assert.DoesNotContain(Space4, searched);
            Assert.DoesNotContain(Space5, searched);
        }

        [Fact]
        public async Task Client_cannot_name_spaces_only_pick_the_kind_of_scope()
        {
            var raw = await (await Login().PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "current", space_ids = new[] { Space3 }, spaceIds = new[] { Space3 } })).Content.ReadAsStringAsync();

            Assert.DoesNotContain("space3-secret", raw);
            Assert.Equal(new[] { Space1 }, _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);

            Assert.Equal(HttpStatusCode.BadRequest, (await Login().PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "3" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Login().PostAsJsonAsync("/api/v1/Chat", new { question = "x", scope = "everything" })).StatusCode);
        }

        [Fact]
        public async Task Another_users_all_scope_never_includes_my_spaces()
        {
            // B 只在預設 Space；A 的其他 Space 不屬於 B
            await JsonAsync(await Login(SeedData.UserB).PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "all" }));

            Assert.Equal(new[] { Space1 }, _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);
        }

        [Fact]
        public async Task Space_restricted_api_token_with_all_scope_stays_inside_its_allowlist()
        {
            var created = await Login(spaceId: null).PostAsJsonAsync("/api/v1/ApiToken", new { name = "t", scopes = new[] { "knowledge.search" }, spaceIDs = new[] { Space2 } });
            var token = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            var pat = _factory.CreateClient();
            pat.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var result = await JsonAsync(await pat.PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "x", scope = "all" }));

            Assert.Equal(new[] { Space2 }, _factory.RagGateway.Calls.Single(c => c.Operation == "search").SearchSpaceIds);
            Assert.Equal(new[] { 42 }, Ids(result.GetProperty("evidence"), "questionID"));
        }

        [Fact]
        public async Task Chat_with_all_scope_searches_every_readable_space_but_the_conversation_belongs_to_the_current_one()
        {
            var answer = await JsonAsync(await Login().PostAsJsonAsync("/api/v1/Chat", new { question = "跨空間", scope = "all" }));

            Assert.Equal(new[] { Space1, Space2 }, _factory.RagGateway.Calls.Single(c => c.Operation == "ask").SearchSpaceIds);
            var chatId = int.Parse(answer.GetProperty("chatID").GetString()!);
            Assert.Equal(Space1, (await _factory.WithDbAsync(db => db.ChatHeader.AsNoTracking().FirstAsync(c => c.ID == chatId))).SpaceID);
        }

        [Fact]
        public async Task Chat_defaults_to_the_current_space_only()
        {
            await JsonAsync(await Login().PostAsJsonAsync("/api/v1/Chat", new { question = "只找這裡" }));

            Assert.Equal(new[] { Space1 }, _factory.RagGateway.Calls.Single(c => c.Operation == "ask").SearchSpaceIds);
        }

        [Fact]
        public async Task Writes_stay_in_the_primary_space_whatever_the_search_scope()
        {
            var response = await Login().PostAsJsonAsync("/api/v1/Question", new { qaItemTypeID = SeedData.QaItemType, question = "寫入", answer = "只進目前空間" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var upsert = _factory.RagGateway.Calls.Single(c => c.Operation == "upsert");
            Assert.Equal(Space1, upsert.SpaceId);
        }
    }
}
