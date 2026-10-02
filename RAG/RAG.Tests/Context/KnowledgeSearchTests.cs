using Microsoft.EntityFrameworkCore;
using RAG.Context;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.Context
{
    /// <summary>
    /// Phase 6 Context Runtime：只檢索、不生成；MySQL 回查狀態；證據與類似答案分級
    /// </summary>
    public class KnowledgeSearchTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const string Url = "/api/v1/Knowledge/search";
        private const int Space1 = SeedData.DefaultSpace;
        private const int Space2 = 2;
        private const int UserViewer = 5;

        private readonly RagApiFactory _factory;

        public KnowledgeSearchTests(RagApiFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.UserCache.Add(new UserCache { ID = UserViewer, Account = "viewer", Password = "v", IsActive = true, Manage = "free" });
                db.Space.Add(new Space { ID = Space2, Key = "team-2", Name = "Team 2", IsActive = true });
                db.AccountSpace.Add(new AccountSpace { UserID = UserViewer, SpaceID = Space1, Role = "viewer", IsActive = true, IsDefault = true });
                db.Questions.AddRange(
                    Q(40, Space1, KnowledgeStatus.Stale, "stale-q", "stale-a"),
                    Q(41, Space1, KnowledgeStatus.Inferred, "inferred-q", "inferred-a"),
                    Q(42, Space1, KnowledgeStatus.Superseded, "old-q", "old-a"),
                    Q(43, Space2, KnowledgeStatus.Verified, "team2-q", "team2-secret"));
                await db.SaveChangesAsync();
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static Questions Q(int id, int spaceId, string status, string question, string answer) =>
            new() { ID = id, QaItemTypeID = SeedData.QaItemType, UserID = SeedData.UserB, SpaceID = spaceId, Status = status, Question = question, Answer = answer, VectorID = $"v{id}" };

        private void Hits(int spaceId, params (int Id, double Score)[] hits) =>
            _factory.RagGateway.SearchHits[spaceId] = hits
                .Select(h => new RagSearchHit { Id = $"v{h.Id}", ReferenceId = h.Id.ToString(), Question = "payload-q", Answer = "payload-answer-must-not-be-used", Score = h.Score })
                .ToList();

        private HttpClient ClientFor(int userId, int? spaceId = null)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private async Task<Result> SearchAsync(object body, int userId = SeedData.UserA, int? spaceId = null)
        {
            var response = await ClientFor(userId, spaceId).PostAsJsonAsync(Url, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Result>())!;
        }

        [Fact]
        public async Task Verified_hits_above_threshold_are_grounded_evidence_with_mysql_content()
        {
            Hits(Space1, (SeedData.QuestionA, 0.92), (SeedData.QuestionB, 0.80));

            var result = await SearchAsync(new { query = "請假" });

            Assert.Equal(KnowledgeSearchStatus.Grounded, result.Status);
            Assert.Equal(new[] { SeedData.QuestionA, SeedData.QuestionB }, result.Evidence.Select(e => e.QuestionID));
            Assert.Equal(new[] { "answer-a", "answer-b" }, result.Evidence.Select(e => e.Answer));
            Assert.All(result.Evidence, e => Assert.Equal(KnowledgeStatus.Verified, e.Status));
            Assert.Empty(result.Similar);
        }

        [Fact]
        public async Task Evidence_far_below_the_best_match_is_demoted_to_similar()
        {
            Hits(Space1, (SeedData.QuestionA, 0.91), (SeedData.QuestionB, 0.63));

            var result = await SearchAsync(new { query = "請假" });

            Assert.Equal(new[] { SeedData.QuestionA }, result.Evidence.Select(e => e.QuestionID));
            Assert.Equal(new[] { SeedData.QuestionB }, result.Similar.Select(s => s.QuestionID));
        }

        [Fact]
        public async Task Superseded_best_match_does_not_raise_the_relative_bar()
        {
            Hits(Space1, (42, 0.99), (SeedData.QuestionA, 0.70));

            var result = await SearchAsync(new { query = "x" });

            Assert.Equal(new[] { SeedData.QuestionA }, result.Evidence.Select(e => e.QuestionID));
        }

        [Fact]
        public async Task Superseded_knowledge_is_never_returned()
        {
            Hits(Space1, (42, 0.99), (SeedData.QuestionA, 0.50));

            var result = await SearchAsync(new { query = "old" });

            Assert.DoesNotContain(result.Evidence.Concat(result.Similar), i => i.QuestionID == 42);
            Assert.Equal(KnowledgeSearchStatus.Insufficient, result.Status);
        }

        [Fact]
        public async Task Only_stale_or_inferred_evidence_is_partial_and_labelled()
        {
            Hits(Space1, (40, 0.9), (41, 0.8));

            var result = await SearchAsync(new { query = "x" });

            Assert.Equal(KnowledgeSearchStatus.Partial, result.Status);
            Assert.Equal(new[] { KnowledgeStatus.Stale, KnowledgeStatus.Inferred }, result.Evidence.Select(e => e.Status));
        }

        [Fact]
        public async Task Low_scores_are_insufficient_with_similar_answers_marked_as_not_evidence()
        {
            Hits(Space1, (SeedData.QuestionA, 0.66), (SeedData.QuestionB, 0.52), (40, 0.48));

            var result = await SearchAsync(new { query = "x" });

            Assert.Equal(KnowledgeSearchStatus.Insufficient, result.Status);
            Assert.Empty(result.Evidence);
            Assert.Equal(new[] { SeedData.QuestionA, SeedData.QuestionB }, result.Similar.Select(s => s.QuestionID));
            Assert.Contains("不可當作依據", result.Notice);
        }

        [Fact]
        public async Task Orphan_vectors_and_records_of_other_spaces_are_dropped()
        {
            Hits(Space1, (999, 0.99), (43, 0.98), (SeedData.QuestionA, 0.9));

            var result = await SearchAsync(new { query = "x" });

            Assert.Equal(new[] { SeedData.QuestionA }, result.Evidence.Select(e => e.QuestionID));
            Assert.DoesNotContain("team2-secret", System.Text.Json.JsonSerializer.Serialize(result));
        }

        [Fact]
        public async Task Search_scope_comes_from_resolved_space_not_request_body()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.AccountSpace.Add(new AccountSpace { UserID = SeedData.UserA, SpaceID = Space2, Role = "member", IsActive = true });
                await db.SaveChangesAsync();
            });
            Hits(Space2, (43, 0.95));

            var result = await SearchAsync(new { query = "x", space_id = Space1, spaceId = Space1 }, spaceId: Space2);

            Assert.Equal(Space2, result.SpaceID);
            Assert.Equal(new[] { 43 }, result.Evidence.Select(e => e.QuestionID));
            var call = Assert.Single(_factory.RagGateway.Calls);
            Assert.Equal(("search", Space2), (call.Operation, call.SpaceId));
        }

        [Fact]
        public async Task Evidence_count_is_bounded()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.Questions.AddRange(Enumerable.Range(50, 8).Select(i => Q(i, Space1, KnowledgeStatus.Verified, $"q{i}", $"a{i}")));
                await db.SaveChangesAsync();
            });
            Hits(Space1, Enumerable.Range(50, 8).Select(i => (i, 0.9)).ToArray());

            var result = await SearchAsync(new { query = "x" });

            Assert.Equal(new ContextRuntimeOptions().MaxEvidence, result.Evidence.Count);
        }

        [Fact]
        public async Task Viewer_can_search_and_access_is_enforced()
        {
            Hits(Space1, (SeedData.QuestionA, 0.9));

            Assert.Equal(KnowledgeSearchStatus.Grounded, (await SearchAsync(new { query = "x" }, userId: UserViewer)).Status);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClientWithToken(null).PostAsJsonAsync(Url, new { query = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(SeedData.UserA, Space2).PostAsJsonAsync(Url, new { query = "x" })).StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Empty_query_is_rejected(string query)
        {
            var response = await ClientFor(SeedData.UserA).PostAsJsonAsync(Url, new { query });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_factory.RagGateway.Calls);
        }

        // ---------- 知識狀態 ----------

        [Fact]
        public async Task Question_status_can_be_set_updated_and_is_validated()
        {
            var client = ClientFor(SeedData.UserA);

            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/Question", new { QaItemTypeID = SeedData.QaItemType, Question = "new", Answer = "a", Status = "Inferred" })).StatusCode);
            Assert.Equal(KnowledgeStatus.Inferred, await _factory.WithDbAsync(db => db.Questions.Where(q => q.Question == "new").Select(q => q.Status).SingleAsync()));

            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/Question", new { QaItemTypeID = SeedData.QaItemType, Question = "default", Answer = "a" })).StatusCode);
            Assert.Equal(KnowledgeStatus.Verified, await _factory.WithDbAsync(db => db.Questions.Where(q => q.Question == "default").Select(q => q.Status).SingleAsync()));

            _factory.RagGateway.Reset();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/Question", new { QaItemTypeID = SeedData.QaItemType, Question = "bad", Answer = "a", Status = "Deleted" })).StatusCode);
            Assert.Empty(_factory.RagGateway.Calls);

            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/Question", new { ID = SeedData.QuestionA, QaItemTypeID = SeedData.QaItemType, Question = "q", Answer = "a", Status = "Superseded" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/Question", new { ID = SeedData.QuestionA, QaItemTypeID = SeedData.QaItemType, Question = "q2", Answer = "a2" })).StatusCode);
            Assert.Equal(KnowledgeStatus.Superseded, (await SeedData.GetQuestionAsync(_factory, SeedData.QuestionA))!.Status);

            var list = await client.GetFromJsonAsync<List<QuestionItem>>("/api/v1/Question?Type=0");
            Assert.Contains(list!, q => q.ID == SeedData.QuestionA && q.Status == KnowledgeStatus.Superseded);
        }

        [Fact]
        public void Invalid_threshold_configuration_fails_fast()
        {
            Assert.Throws<InvalidOperationException>(() => new ContextRuntimeOptions { EvidenceScoreThreshold = 0.5, SimilarScoreThreshold = 0.7 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new ContextRuntimeOptions { MaxTokenBudget = 10 }.Validate());
            Assert.Throws<InvalidOperationException>(() => new ContextRuntimeOptions { EvidenceRelativeMargin = -0.1 }.Validate());
        }

        private sealed record Item(int QuestionID, string Question, string Answer, double Score, string Status);

        private sealed record Result(int SpaceID, string SpaceKey, string Status, List<Item> Evidence, List<Item> Similar, string Notice);

        private sealed record QuestionItem(int ID, string Status);
    }
}
