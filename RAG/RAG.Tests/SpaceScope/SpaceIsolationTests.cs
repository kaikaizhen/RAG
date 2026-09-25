using Microsoft.EntityFrameworkCore;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.SpaceScope
{
    /// <summary>
    /// Phase 3：Chat / Question / RAG 呼叫依 SpaceContext 隔離（真實 pipeline + 記錄 scope 的 fake gateway）
    /// </summary>
    public class SpaceIsolationTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private const int Space1 = SeedData.DefaultSpace;
        private const int Space2 = 2;
        private const int UserViewer = 5;

        // A 在 Space 2 的資料（A 在 Space 1 的資料來自 SeedData：ChatA=10、QuestionA=31）
        private const int ChatA_Space2 = 12;
        private const int QuestionA_Space2 = 32;

        private readonly RagApiFactory _factory;

        public SpaceIsolationTests(RagApiFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.UserCache.Add(new UserCache { ID = UserViewer, Account = "viewer", Password = "pv", IsActive = true, Manage = "free" });
                db.Space.Add(new Space { ID = Space2, Key = "team-2", Name = "Team 2", IsActive = true });
                db.AccountSpace.AddRange(
                    new AccountSpace { UserID = SeedData.UserA, SpaceID = Space2, Role = "member", IsActive = true },
                    new AccountSpace { UserID = UserViewer, SpaceID = Space1, Role = "viewer", IsActive = true, IsDefault = true });
                db.ChatHeader.Add(new ChatHeader { ID = ChatA_Space2, Name = "chat-a-2", UserID = SeedData.UserA, SpaceID = Space2, CreateAt = DateTime.Now });
                db.ChatLog.Add(new ChatLog { ID = 120, ChatHeaderID = ChatA_Space2, Questions = "q2", Answer = "a2" });
                db.Questions.Add(new Questions { ID = QuestionA_Space2, QaItemTypeID = SeedData.QaItemType, UserID = SeedData.UserA, SpaceID = Space2, Question = "question-a-2", Answer = "answer-a-2", VectorID = "va2" });
                await db.SaveChangesAsync();
            });
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient ClientFor(int userId, int? spaceId = null)
        {
            var client = _factory.CreateClientWithToken(TestTokens.Create(_factory.SecretKey, subject: userId.ToString()));
            if (spaceId is int id) client.DefaultRequestHeaders.Add("X-Space-Id", id.ToString());
            return client;
        }

        private List<FakeRagGateway.Call> Calls => _factory.RagGateway.Calls.ToList();

        // ---------- MySQL：讀取僅限目前 Space ----------

        [Theory]
        [InlineData(Space1, SeedData.QuestionA)]
        [InlineData(Space2, QuestionA_Space2)]
        public async Task Question_list_only_contains_current_space(int spaceId, int expectedQuestion)
        {
            var questions = await ClientFor(SeedData.UserA, spaceId).GetFromJsonAsync<List<QuestionItem>>("/api/Question?Type=0");

            Assert.Equal(new[] { expectedQuestion }, questions!.Select(q => q.ID));
        }

        [Theory]
        [InlineData(Space1, SeedData.ChatA)]
        [InlineData(Space2, ChatA_Space2)]
        public async Task Chat_history_only_contains_current_space(int spaceId, int expectedChat)
        {
            var chats = await ClientFor(SeedData.UserA, spaceId).GetFromJsonAsync<List<ChatItem>>("/api/Chat");

            Assert.Equal(new[] { expectedChat.ToString() }, chats!.Select(c => c.ID));
        }

        // ---------- 以猜測 ID 跨 Space 讀 / 改 / 刪一律 404，且不執行 mutation 或 RAG 呼叫 ----------

        [Fact]
        public async Task Cannot_read_chat_log_of_other_space()
        {
            var response = await ClientFor(SeedData.UserA, Space2).GetAsync($"/api/Chat/{SeedData.ChatA}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Cannot_delete_chat_of_other_space()
        {
            var response = await ClientFor(SeedData.UserA, Space2).DeleteAsync($"/api/Chat/{SeedData.ChatA}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(await SeedData.ChatExistsAsync(_factory, SeedData.ChatA));
        }

        [Fact]
        public async Task Cannot_append_to_chat_of_other_space_and_rag_is_not_called()
        {
            var response = await ClientFor(SeedData.UserA, Space2).PostAsJsonAsync("/api/Chat", new { Question = "q", ChatID = SeedData.ChatA.ToString() });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(Calls);
            Assert.Equal(1, await SeedData.ChatLogCountAsync(_factory, SeedData.ChatA));
        }

        [Fact]
        public async Task Cannot_update_question_of_other_space()
        {
            var response = await ClientFor(SeedData.UserA, Space2).PutAsJsonAsync("/api/Question", new
            {
                ID = SeedData.QuestionA, QaItemTypeID = SeedData.QaItemType, Question = "hacked", Answer = "hacked"
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(Calls);
            Assert.Equal("question-a", (await SeedData.GetQuestionAsync(_factory, SeedData.QuestionA))!.Question);
        }

        [Fact]
        public async Task Cannot_delete_question_of_other_space()
        {
            var response = await ClientFor(SeedData.UserA, Space2).DeleteAsync($"/api/Question/{SeedData.QuestionA}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(Calls);
            Assert.NotNull(await SeedData.GetQuestionAsync(_factory, SeedData.QuestionA));
        }

        // ---------- RAG scope 只來自伺服器端 SpaceContext ----------

        [Fact]
        public async Task Ask_uses_resolved_space_and_ignores_body_space_id()
        {
            var response = await ClientFor(SeedData.UserA, Space2).PostAsJsonAsync("/api/Chat", new { Question = "hello", space_id = Space1, SpaceId = Space1 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var call = Assert.Single(Calls);
            Assert.Equal("ask", call.Operation);
            Assert.Equal(Space2, call.SpaceId);

            var newChat = await _factory.WithDbAsync(db => db.ChatHeader.AsNoTracking().OrderByDescending(c => c.ID).FirstAsync());
            Assert.Equal(Space2, newChat.SpaceID);
        }

        [Fact]
        public async Task Create_question_is_stored_and_vectorized_in_resolved_space_only()
        {
            var response = await ClientFor(SeedData.UserA, Space2).PostAsJsonAsync("/api/Question", new
            {
                QaItemTypeID = SeedData.QaItemType, Question = "new-q", Answer = "new-a", SpaceID = Space1, space_id = Space1
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var call = Assert.Single(Calls);
            Assert.Equal(("upsert", Space2), (call.Operation, call.SpaceId));

            var created = await _factory.WithDbAsync(db => db.Questions.AsNoTracking().SingleAsync(q => q.Question == "new-q"));
            Assert.Equal(Space2, created.SpaceID);
            Assert.Equal(SeedData.UserA, created.UserID);
            Assert.Equal(call.VectorId, created.VectorID);
        }

        [Fact]
        public async Task Update_revectorizes_new_content_into_same_point_and_space()
        {
            var response = await ClientFor(SeedData.UserA, Space2).PutAsJsonAsync("/api/Question", new
            {
                ID = QuestionA_Space2, QaItemTypeID = SeedData.QaItemType, Question = "updated-q", Answer = "updated-a"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var call = Assert.Single(Calls);
            Assert.Equal(Space2, call.SpaceId);
            Assert.Equal("va2", call.Question!.ReferenceID);
            Assert.Equal("updated-q", call.Question.Question);

            var updated = await SeedData.GetQuestionAsync(_factory, QuestionA_Space2);
            Assert.Equal(("updated-q", Space2, "va2"), (updated!.Question, updated.SpaceID, updated.VectorID));
        }

        [Fact]
        public async Task Delete_question_removes_vector_in_its_space_then_row()
        {
            var response = await ClientFor(SeedData.UserA, Space2).DeleteAsync($"/api/Question/{QuestionA_Space2}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var call = Assert.Single(Calls);
            Assert.Equal(("delete", Space2, "va2"), (call.Operation, call.SpaceId, call.VectorId));
            Assert.Null(await SeedData.GetQuestionAsync(_factory, QuestionA_Space2));
        }

        // ---------- 權限 ----------

        [Fact]
        public async Task Viewer_can_read_and_ask_but_cannot_write_knowledge()
        {
            var client = ClientFor(UserViewer);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Question?Type=0")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Chat", new { Question = "hello" })).StatusCode);
            _factory.RagGateway.Reset();

            var create = await client.PostAsJsonAsync("/api/Question", new { QaItemTypeID = SeedData.QaItemType, Question = "q", Answer = "a" });

            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
            Assert.Empty(Calls);
            Assert.False(await _factory.WithDbAsync(db => db.Questions.AnyAsync(q => q.UserID == UserViewer)));
        }

        // ---------- 跨 store 一致性：失敗可補償 / 可重試 ----------

        [Fact]
        public async Task Failed_vectorization_leaves_no_question_row()
        {
            _factory.RagGateway.FailUpsert = true;

            var response = await ClientFor(SeedData.UserA, Space1).PostAsJsonAsync("/api/Question", new { QaItemTypeID = SeedData.QaItemType, Question = "will-fail", Answer = "a" });

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.False(await _factory.WithDbAsync(db => db.Questions.AnyAsync(q => q.Question == "will-fail")));
        }

        [Fact]
        public async Task Failed_vector_delete_keeps_question_row_for_retry()
        {
            _factory.RagGateway.FailDelete = true;
            var client = ClientFor(SeedData.UserA, Space1);

            var failed = await client.DeleteAsync($"/api/Question/{SeedData.QuestionA}");
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.NotNull(await SeedData.GetQuestionAsync(_factory, SeedData.QuestionA));

            _factory.RagGateway.FailDelete = false;
            var retried = await client.DeleteAsync($"/api/Question/{SeedData.QuestionA}");
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            Assert.Null(await SeedData.GetQuestionAsync(_factory, SeedData.QuestionA));
        }

        // ---------- 全域 QaType：禁止跨 Space 連帶刪除 ----------

        [Fact]
        public async Task Deleting_qa_type_or_item_in_use_is_refused_without_side_effects()
        {
            var client = ClientFor(SeedData.UserA);

            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/QaType/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/QaTypeItem/{SeedData.QaItemType}")).StatusCode);

            Assert.Empty(Calls);
            Assert.Equal(3, await _factory.WithDbAsync(db => db.Questions.CountAsync()));
            Assert.True(await _factory.WithDbAsync(db => db.QaItemType.AnyAsync(i => i.ID == SeedData.QaItemType)));
        }

        [Fact]
        public async Task Deleting_unused_qa_type_item_succeeds()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.QaItemType.Add(new QaItemType { ID = 9, QaTypeID = 1, NameZh = "unused", IsActive = true });
                await db.SaveChangesAsync();
            });

            var response = await ClientFor(SeedData.UserA).DeleteAsync("/api/QaTypeItem/9");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(Calls);
        }

        // ---------- 帳號刪除：以每筆紀錄自身的 Space 清除向量 ----------

        [Fact]
        public async Task Deleting_self_removes_vectors_with_each_records_own_space()
        {
            var response = await ClientFor(SeedData.UserA).DeleteAsync($"/api/User/{SeedData.UserA}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var deletes = Calls.Where(c => c.Operation == "delete").Select(c => (VectorId: c.VectorId!, c.SpaceId)).OrderBy(x => x.VectorId).ToList();
            Assert.Equal(new[] { ("va", Space1), ("va2", Space2) }, deletes);
        }

        private sealed record QuestionItem(int ID, int UserID);

        private sealed record ChatItem(string ID, string Name);
    }
}
