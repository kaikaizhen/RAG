using Microsoft.EntityFrameworkCore;
using RAG.Authentication.ApiTokens;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RAG.Tests.Platform
{
    /// <summary>
    /// 應用程式權杖與暫存區：權杖只能搜尋與放進暫存區，不能直接寫入知識庫；接受草稿才會產生向量
    /// </summary>
    public class ApiTokenAndInboxTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const int Space1 = SeedData.DefaultSpace;
        private const int Space2 = 2;
        private const int UserViewer = 5;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly RagApiFactory _factory;

        public ApiTokenAndInboxTests(RagApiFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.UserCache.Add(new UserCache { ID = UserViewer, Account = "viewer", Password = "v", IsActive = true, Manage = "free" });
                db.Space.Add(new Space { ID = Space2, Key = "team-2", Name = "Team 2", IsActive = true });
                db.AccountSpace.AddRange(
                    new AccountSpace { UserID = UserViewer, SpaceID = Space1, Role = "viewer", IsActive = true, IsDefault = true },
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = Space2, Role = "member", IsActive = true, IsDefault = false });
                await db.SaveChangesAsync();
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient Login(int userId, int? spaceId = null)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private HttpClient WithToken(string token, int? spaceId = null)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private async Task<string> CreateTokenAsync(int userId = SeedData.UserA, string[]? scopes = null, int[]? spaces = null, int? expiresInDays = null)
        {
            var response = await Login(userId).PostAsJsonAsync("/api/v1/ApiToken", new { name = "test", scopes = scopes ?? new[] { "knowledge.search", "inbox.write", "space.read" }, spaceIDs = spaces, expiresInDays });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        }

        private static object Draft(string question = "q?", string answer = "a.") => new { question, answer };

        [Fact]
        public async Task Plaintext_is_returned_once_and_only_the_hash_is_stored()
        {
            var token = await CreateTokenAsync();

            Assert.StartsWith("rag_pat_", token);
            var stored = await _factory.WithDbAsync(db => db.ApiToken.AsNoTracking().ToListAsync());
            Assert.Single(stored);
            Assert.DoesNotContain(token, stored[0].TokenHash);
            Assert.NotEqual(token, stored[0].TokenHash);
            Assert.Equal(token[..12], stored[0].TokenPrefix);

            var list = await Login(SeedData.UserA).GetStringAsync("/api/v1/ApiToken");
            Assert.DoesNotContain(token, list);
            Assert.Contains(stored[0].TokenPrefix, list);
        }

        [Fact]
        public async Task Token_cannot_be_created_with_unknown_scope_or_foreign_space()
        {
            var client = Login(SeedData.UserA);

            var badScope = await client.PostAsJsonAsync("/api/v1/ApiToken", new { name = "x", scopes = new[] { "knowledge.write" } });
            Assert.Equal(HttpStatusCode.BadRequest, badScope.StatusCode);

            var foreign = await client.PostAsJsonAsync("/api/v1/ApiToken", new { name = "x", scopes = new[] { "knowledge.search" }, spaceIDs = new[] { 99 } });
            Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        }

        [Fact]
        public async Task Token_works_only_on_endpoints_that_opt_in_and_never_manages_tokens()
        {
            var token = await CreateTokenAsync();
            var client = WithToken(token, Space1);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/Space")).StatusCode);

            // 沒有掛 ApiPolicies 的 endpoint：權杖等同未登入
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/Question?Type=0")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/Question", new { qaItemTypeID = 1, question = "q", answer = "a" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/ApiToken")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/ApiToken", new { name = "x", scopes = new[] { "inbox.write" } })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/Chat")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/Inbox/1/accept", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/Inbox")).StatusCode);
        }

        [Fact]
        public async Task Scope_is_required_per_endpoint()
        {
            var searchOnly = WithToken(await CreateTokenAsync(scopes: new[] { "knowledge.search" }), Space1);

            Assert.Equal(HttpStatusCode.OK, (await searchOnly.PostAsJsonAsync("/api/v1/Knowledge/search", new { query = "請假" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await searchOnly.PostAsJsonAsync("/api/v1/Inbox", Draft())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await searchOnly.GetAsync("/api/v1/Space")).StatusCode);
        }

        [Fact]
        public async Task Token_posting_to_inbox_never_touches_the_knowledge_base_or_vectors()
        {
            var client = WithToken(await CreateTokenAsync(), Space1);

            var response = await client.PostAsJsonAsync("/api/v1/Inbox", new { question = "新問題", answer = "新答案", sourceRef = "note#1" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Pending", draft.GetProperty("status").GetString());
            Assert.Equal("test", draft.GetProperty("source").GetString());
            Assert.Equal("note#1", draft.GetProperty("sourceRef").GetString());
            Assert.DoesNotContain(_factory.RagGateway.Calls, c => c.Operation == "upsert");
            Assert.False(await _factory.WithDbAsync(db => db.Questions.AnyAsync(q => q.Question == "新問題")));
        }

        [Fact]
        public async Task Same_content_is_not_piled_up_twice()
        {
            var client = WithToken(await CreateTokenAsync(), Space1);

            var first = await (await client.PostAsJsonAsync("/api/v1/Inbox", Draft("重複", "內容"))).Content.ReadFromJsonAsync<JsonElement>();
            var second = await (await client.PostAsJsonAsync("/api/v1/Inbox", Draft("重複", "內容"))).Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(first.GetProperty("id").GetInt32(), second.GetProperty("id").GetInt32());
            Assert.Equal(1, await _factory.WithDbAsync(db => db.KnowledgeDraft.CountAsync()));
        }

        [Fact]
        public async Task Space_restricted_token_cannot_reach_other_spaces()
        {
            var client = WithToken(await CreateTokenAsync(spaces: new[] { Space2 }));

            // 只限一個 Space 時，不帶 X-Space-Id 就用那個 Space
            var ok = await client.PostAsJsonAsync("/api/v1/Inbox", Draft());
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal(Space2, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("spaceID").GetInt32());

            var other = WithToken(await CreateTokenAsync(spaces: new[] { Space2 }), Space1);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/Inbox", Draft("b", "b"))).StatusCode);

            var spaces = await client.GetFromJsonAsync<JsonElement>("/api/v1/Space");
            Assert.Equal(new[] { Space2 }, spaces.EnumerateArray().Select(s => s.GetProperty("spaceID").GetInt32()));
        }

        [Fact]
        public async Task Read_only_member_cannot_fill_the_inbox()
        {
            var client = WithToken(await CreateTokenAsync(UserViewer), Space1);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/Inbox", Draft())).StatusCode);
        }

        [Fact]
        public async Task Revoked_expired_and_disabled_user_tokens_are_rejected()
        {
            var revoked = await CreateTokenAsync();
            var id = (await _factory.WithDbAsync(db => db.ApiToken.AsNoTracking().FirstAsync())).ID;
            Assert.Equal(HttpStatusCode.OK, (await Login(SeedData.UserA).DeleteAsync($"/api/v1/ApiToken/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await WithToken(revoked, Space1).GetAsync("/api/v1/Space")).StatusCode);

            var expired = await CreateTokenAsync();
            await _factory.WithDbAsync(async db =>
            {
                var t = await db.ApiToken.OrderByDescending(x => x.ID).FirstAsync();
                t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.Unauthorized, (await WithToken(expired, Space1).GetAsync("/api/v1/Space")).StatusCode);

            var disabled = await CreateTokenAsync();
            await _factory.WithDbAsync(async db =>
            {
                (await db.UserCache.FirstAsync(u => u.ID == SeedData.UserA)).IsActive = false;
                await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.Unauthorized, (await WithToken(disabled, Space1).GetAsync("/api/v1/Space")).StatusCode);

            Assert.Equal(HttpStatusCode.Unauthorized, (await WithToken("rag_pat_notarealtoken", Space1).GetAsync("/api/v1/Space")).StatusCode);
        }

        [Fact]
        public async Task Tokens_belong_to_their_owner()
        {
            await CreateTokenAsync(SeedData.UserA);
            var id = (await _factory.WithDbAsync(db => db.ApiToken.AsNoTracking().FirstAsync())).ID;

            Assert.Equal(HttpStatusCode.NotFound, (await Login(SeedData.UserB).DeleteAsync($"/api/v1/ApiToken/{id}")).StatusCode);
            Assert.Equal("[]", (await Login(SeedData.UserB).GetStringAsync("/api/v1/ApiToken")));
        }

        [Fact]
        public async Task Accepting_a_draft_creates_the_question_and_vector_as_inferred()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(), "怎麼報帳？", "找財務。");

            var response = await Login(SeedData.UserA, Space1).PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Accepted", draft.GetProperty("status").GetString());
            var questionId = draft.GetProperty("acceptedQuestionID").GetInt32();
            var question = await SeedData.GetQuestionAsync(_factory, questionId);
            Assert.Equal("怎麼報帳？", question!.Question);
            Assert.Equal(KnowledgeStatus.Inferred, question.Status);
            Assert.Equal(Space1, question.SpaceID);
            Assert.Single(_factory.RagGateway.Calls, c => c.Operation == "upsert" && c.SpaceId == Space1);

            // 不能重複接受
            var again = await Login(SeedData.UserA, Space1).PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType });
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        [Fact]
        public async Task Double_clicking_accept_creates_exactly_one_question()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(), "連點", "只該有一筆");
            var user = Login(SeedData.UserA, Space1);

            var responses = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => user.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType })));

            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            Assert.Equal(1, await _factory.WithDbAsync(db => db.Questions.CountAsync(q => q.Question == "連點")));
            Assert.Single(_factory.RagGateway.Calls, c => c.Operation == "upsert");
        }

        [Fact]
        public async Task A_stuck_accepting_draft_can_be_claimed_again_after_the_timeout()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(), "卡住", "內容");
            await _factory.WithDbAsync(async db =>
            {
                var d = await db.KnowledgeDraft.FirstAsync(x => x.ID == draftId);
                d.Status = DraftStatus.Accepting;
                d.ReviewedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            });
            var user = Login(SeedData.UserA, Space1);

            // 剛被認領：處理中，不能再接受
            Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType })).StatusCode);

            await _factory.WithDbAsync(async db =>
            {
                (await db.KnowledgeDraft.FirstAsync(x => x.ID == draftId)).ReviewedAt = DateTime.UtcNow.AddMinutes(-10);
                await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.OK, (await user.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType })).StatusCode);
        }

        [Fact]
        public async Task Accepting_needs_a_type_and_a_failed_vector_write_leaves_the_draft_pending()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(), "q", "a");
            var user = Login(SeedData.UserA, Space1);

            Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { })).StatusCode);

            _factory.RagGateway.FailUpsert = true;
            Assert.Equal(HttpStatusCode.InternalServerError, (await user.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType })).StatusCode);
            _factory.RagGateway.FailUpsert = false;

            var drafts = await user.GetFromJsonAsync<JsonElement>("/api/v1/Inbox");
            Assert.Equal(draftId, drafts.EnumerateArray().Single().GetProperty("id").GetInt32());
            Assert.False(await _factory.WithDbAsync(db => db.Questions.AnyAsync(x => x.Question == "q")));
        }

        [Fact]
        public async Task Drafts_are_private_to_their_owner_and_space()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(SeedData.UserA), "私人", "內容");

            // 同一個 Space 的另一位成員看不到、也不能審查
            var other = Login(SeedData.UserB, Space1);
            Assert.Equal("[]", await other.GetStringAsync("/api/v1/Inbox"));
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/Inbox/{draftId}/accept", new { qaItemTypeID = SeedData.QaItemType })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/Inbox/{draftId}/reject", null)).StatusCode);

            // 本人換到別的 Space 也看不到
            Assert.Equal("[]", await Login(SeedData.UserA, Space2).GetStringAsync("/api/v1/Inbox"));
        }

        [Fact]
        public async Task Rejecting_and_editing_work_on_pending_drafts_only()
        {
            var draftId = await PostDraftAsync(await CreateTokenAsync(), "舊", "舊內容");
            var user = Login(SeedData.UserA, Space1);

            var edited = await user.PutAsJsonAsync($"/api/v1/Inbox/{draftId}", new { question = "新", answer = "新內容", qaItemTypeID = SeedData.QaItemType });
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            Assert.Equal("新", (await edited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("question").GetString());

            Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/api/v1/Inbox/{draftId}/reject", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync($"/api/v1/Inbox/{draftId}/reject", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await user.PutAsJsonAsync($"/api/v1/Inbox/{draftId}", new { question = "x", answer = "y" })).StatusCode);
            Assert.DoesNotContain(_factory.RagGateway.Calls, c => c.Operation == "upsert");

            var rejected = await user.GetFromJsonAsync<JsonElement>("/api/v1/Inbox?status=Rejected");
            Assert.Single(rejected.EnumerateArray());
        }

        [Fact]
        public async Task Pending_inbox_has_a_cap()
        {
            await _factory.WithDbAsync(async db =>
            {
                for (var i = 0; i < RAG.Inbox.InboxService.MaxPendingPerSpace; i++)
                {
                    db.KnowledgeDraft.Add(new KnowledgeDraft { SpaceID = Space1, UserID = SeedData.UserA, Question = $"q{i}", Answer = "a", Source = "x", ContentHash = $"h{i}", Status = DraftStatus.Pending, CreateAt = DateTime.UtcNow });
                }
                await db.SaveChangesAsync();
            });

            var response = await WithToken(await CreateTokenAsync(), Space1).PostAsJsonAsync("/api/v1/Inbox", Draft("one more", "x"));

            Assert.Equal((HttpStatusCode)429, response.StatusCode);
        }

        private async Task<int> PostDraftAsync(string token, string question, string answer)
        {
            var response = await WithToken(token, Space1).PostAsJsonAsync("/api/v1/Inbox", Draft(question, answer));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }
    }
}
