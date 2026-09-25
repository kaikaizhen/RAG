using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.Authentication
{
    /// <summary>
    /// User A 以猜測的 ID 存取 User B 的資源：一律 404（與不存在相同），且不得執行 mutation
    /// </summary>
    public class OwnershipIsolationTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;
        private readonly HttpClient _clientA;

        public OwnershipIsolationTests(RagApiFactory factory)
        {
            _factory = factory;
            _clientA = factory.CreateClientWithToken(TestTokens.Create(factory.SecretKey, subject: SeedData.UserA.ToString()));
        }

        public Task InitializeAsync() => SeedData.ResetAsync(_factory);

        public Task DisposeAsync() => Task.CompletedTask;

        // ---------- User ----------

        [Fact]
        public async Task Cannot_read_other_user()
        {
            var response = await _clientA.GetAsync($"/api/v1/User/{SeedData.UserB}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Cannot_update_other_user()
        {
            var response = await _clientA.PutAsJsonAsync("/api/v1/User", new
            {
                ID = SeedData.UserB, Account = "hacked", Password = "hacked", NameZh = "hacked", Manage = "admin"
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var userB = await SeedData.GetUserAsync(_factory, SeedData.UserB);
            Assert.Equal("userB", userB!.Account);
            Assert.Equal("passB", userB.Password);
        }

        [Fact]
        public async Task Cannot_delete_other_user()
        {
            var response = await _clientA.DeleteAsync($"/api/v1/User/{SeedData.UserB}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotNull(await SeedData.GetUserAsync(_factory, SeedData.UserB));
        }

        [Fact]
        public async Task Can_update_self_but_not_escalate_privilege_fields()
        {
            var response = await _clientA.PutAsJsonAsync("/api/v1/User", new
            {
                ID = SeedData.UserA, Account = SeedData.UserAAccount, Password = "newpass", NameZh = "A2", Manage = "admin", IsActive = false
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var userA = await SeedData.GetUserAsync(_factory, SeedData.UserA);
            Assert.Equal("newpass", userA!.Password);
            Assert.Equal("free", userA.Manage);
            Assert.True(userA.IsActive);
        }

        // ---------- Chat / ChatLog ----------

        [Fact]
        public async Task Chat_history_is_scoped_to_caller_and_ignores_query_UserID()
        {
            var response = await _clientA.GetAsync($"/api/v1/Chat?UserID={SeedData.UserB}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var chats = await response.Content.ReadFromJsonAsync<List<ChatItem>>();
            Assert.Equal(new[] { SeedData.ChatA.ToString() }, chats!.Select(c => c.ID));
        }

        [Fact]
        public async Task Cannot_read_other_users_chat_log()
        {
            var response = await _clientA.GetAsync($"/api/v1/Chat/{SeedData.ChatB}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Can_read_own_chat_log()
        {
            var response = await _clientA.GetAsync($"/api/v1/Chat/{SeedData.ChatA}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var logs = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Single(logs!);
        }

        [Fact]
        public async Task Cannot_append_to_other_users_chat()
        {
            // 擁有權檢查在呼叫 RAG Server 之前，因此測試環境無 RAG Server 仍應得到 404 而非 500
            var response = await _clientA.PostAsJsonAsync("/api/v1/Chat", new { Question = "q", ChatID = SeedData.ChatB.ToString(), UserID = SeedData.UserB });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(1, await SeedData.ChatLogCountAsync(_factory, SeedData.ChatB));
        }

        [Fact]
        public async Task Cannot_stream_into_other_users_chat()
        {
            var response = await _clientA.PostAsJsonAsync("/api/v1/Chat/ChatStream", new { Question = "q", ChatID = SeedData.ChatB.ToString() });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(1, await SeedData.ChatLogCountAsync(_factory, SeedData.ChatB));
        }

        [Fact]
        public async Task Cannot_delete_other_users_chat()
        {
            var response = await _clientA.DeleteAsync($"/api/v1/Chat/{SeedData.ChatB}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(await SeedData.ChatExistsAsync(_factory, SeedData.ChatB));
            Assert.Equal(1, await SeedData.ChatLogCountAsync(_factory, SeedData.ChatB));
        }

        [Fact]
        public async Task Can_delete_own_chat_with_logs()
        {
            var response = await _clientA.DeleteAsync($"/api/v1/Chat/{SeedData.ChatA}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(await SeedData.ChatExistsAsync(_factory, SeedData.ChatA));
            Assert.Equal(0, await SeedData.ChatLogCountAsync(_factory, SeedData.ChatA));
        }

        // ---------- Question ----------

        [Theory]
        [InlineData("/api/v1/Question?Type=0&UserID=2")]
        [InlineData("/api/v1/Question?Type=1&Id=1&UserID=2")]
        public async Task Question_list_is_scoped_to_caller_and_ignores_query_UserID(string url)
        {
            var response = await _clientA.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var questions = await response.Content.ReadFromJsonAsync<List<QuestionItem>>();
            Assert.Equal(new[] { SeedData.QuestionA }, questions!.Select(q => q.ID));
        }

        [Fact]
        public async Task Cannot_update_other_users_question()
        {
            var response = await _clientA.PutAsJsonAsync("/api/v1/Question", new
            {
                ID = SeedData.QuestionB, QaItemTypeID = SeedData.QaItemType, UserID = SeedData.UserB, Question = "hacked", Answer = "hacked"
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var question = await SeedData.GetQuestionAsync(_factory, SeedData.QuestionB);
            Assert.Equal("question-b", question!.Question);
            Assert.Equal(SeedData.UserB, question.UserID);
        }

        [Fact]
        public async Task Cannot_delete_other_users_question()
        {
            var response = await _clientA.DeleteAsync($"/api/v1/Question/{SeedData.QuestionB}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotNull(await SeedData.GetQuestionAsync(_factory, SeedData.QuestionB));
        }

        // ---------- 一致的安全語意：非本人 = 不存在 ----------

        [Theory]
        [InlineData("GET", "/api/v1/User/999")]
        [InlineData("DELETE", "/api/v1/User/999")]
        [InlineData("GET", "/api/v1/Chat/999")]
        [InlineData("DELETE", "/api/v1/Chat/999")]
        [InlineData("DELETE", "/api/v1/Question/999")]
        public async Task Nonexistent_resource_returns_same_404_as_foreign_resource(string method, string url)
        {
            var response = await _clientA.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private sealed record ChatItem(string ID, string Name);

        private sealed record QuestionItem(int ID, int UserID);
    }
}
