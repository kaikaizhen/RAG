using Microsoft.Extensions.DependencyInjection;
using RAG.Models.Entities;
using RAG.SpaceRuntime;
using RAG.Tests.Infrastructure;
using RAG.ToolRuntime;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RAG.Tests.ToolRuntime
{
    /// <summary>
    /// 真實 DI / pipeline：capability 清單與 knowledge.search 使用伺服器端解析的 Space
    /// </summary>
    public class ToolApiIntegrationTests : IClassFixture<RagApiFactory>, IAsyncLifetime
    {
        private readonly RagApiFactory _factory;

        public ToolApiIntegrationTests(RagApiFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.Space.Add(new Space { ID = 2, Key = "team-2", Name = "Team 2", IsActive = true });
                db.AccountSpace.Add(new AccountSpace { UserID = SeedData.UserA, SpaceID = 2, Role = "owner", IsActive = true });
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

        [Fact]
        public async Task Capability_list_reflects_space_allow_list()
        {
            var inDefault = await ClientFor(SeedData.UserA).GetFromJsonAsync<List<Capability>>("/api/v1/Capability");
            var inTeam2 = await ClientFor(SeedData.UserA, 2).GetFromJsonAsync<List<Capability>>("/api/v1/Capability");

            Assert.Equal(new[] { ("knowledge.search", "read") }, inDefault!.Select(c => (c.Id, c.Kind)));
            Assert.Empty(inTeam2!);
        }

        [Fact]
        public async Task Capability_list_requires_authentication_and_membership()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClientWithToken(null).GetAsync("/api/v1/Capability")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(SeedData.UserB, 2).GetAsync("/api/v1/Capability")).StatusCode);
        }

        [Fact]
        public async Task There_is_no_http_endpoint_to_invoke_tools()
        {
            var response = await ClientFor(SeedData.UserA).PostAsJsonAsync("/api/v1/Capability/knowledge.search/invoke", new { query = "x" });

            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task Knowledge_search_uses_rag_scope_of_resolved_context_and_is_denied_where_not_allowed()
        {
            using var scope = _factory.Services.CreateScope();
            var spaces = scope.ServiceProvider.GetRequiredService<IAccountSpaceResolver>();
            var invoker = scope.ServiceProvider.GetRequiredService<IToolInvoker>();
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"query\":\"請假流程\"}")!;

            var defaultContext = new SpaceExecutionContext((await spaces.ResolveAsync(SeedData.UserA, SeedData.DefaultSpace)).Context!, ResourceScope.Empty);
            var team2Context = new SpaceExecutionContext((await spaces.ResolveAsync(SeedData.UserA, 2)).Context!, ResourceScope.Empty);

            var allowed = await invoker.InvokeAsync(defaultContext, "knowledge.search", args, "it-1");
            var denied = await invoker.InvokeAsync(team2Context, "knowledge.search", args, "it-2");

            Assert.Equal(ToolInvocationStatus.Succeeded, allowed.Status);
            Assert.Equal(ToolInvocationStatus.Forbidden, denied.Status);
            var call = Assert.Single(_factory.RagGateway.Calls);
            Assert.Equal(("ask", SeedData.DefaultSpace, "請假流程"), (call.Operation, call.SpaceId, call.AskQuestion));
        }

        private sealed record Capability(string Id, string Description, string Kind);
    }
}
