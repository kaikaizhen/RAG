using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using RAG.Tests.SkillRuntime;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.Context
{
    /// <summary>
    /// Phase 6：Skill / Workflow 內容包（受 token 預算限制、只含該 Space 的有效 Skill）
    /// </summary>
    public class SkillBundleTests : IClassFixture<SkillApiFactory>, IAsyncLifetime
    {
        private const string Url = "/api/v1/Skill/bundle";
        private readonly SkillApiFactory _factory;

        public SkillBundleTests(SkillApiFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await SeedData.ResetAsync(_factory);
            await _factory.WithDbAsync(async db =>
            {
                db.Space.Add(new Space { ID = 2, Key = "team-2", Name = "Team 2", IsActive = true });
                db.AccountSpace.Add(new AccountSpace { UserID = SeedData.UserA, SpaceID = 2, Role = "viewer", IsActive = true });
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
        public async Task Default_space_gets_default_skill_and_workflow_content()
        {
            var bundle = await ClientFor(SeedData.UserA).GetFromJsonAsync<Bundle>(Url);

            Assert.Equal(new[] { ("rapid", "# default rules") }, bundle!.Skills.Select(s => (s.Id, s.Content)));
            Assert.Equal("default-flow", bundle.Workflow!.Id);
            Assert.Equal(8000, bundle.TokenBudget);
            Assert.DoesNotContain("team 2 rules", System.Text.Json.JsonSerializer.Serialize(bundle));
        }

        [Fact]
        public async Task Space_specific_content_is_only_delivered_to_that_space()
        {
            var bundle = await ClientFor(SeedData.UserA, 2).GetFromJsonAsync<Bundle>(Url);

            Assert.Equal(new[] { "rapid", "team-2-skill" }, bundle!.Skills.Select(s => s.Id));
            Assert.Contains(bundle.Skills, s => s.Content == "# team 2 rules");
        }

        [Fact]
        public async Task Token_budget_limits_loaded_content()
        {
            var bundle = await ClientFor(SeedData.UserA, 2).GetFromJsonAsync<Bundle>($"{Url}?tokenBudget=5");

            Assert.True(bundle!.TokenEstimate <= 5);
            Assert.NotEmpty(bundle.OmittedSkillIds);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(32001)]
        public async Task Invalid_token_budget_is_400(int budget)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await ClientFor(SeedData.UserA).GetAsync($"{Url}?tokenBudget={budget}")).StatusCode);
        }

        [Fact]
        public async Task Access_is_enforced()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClientWithToken(null).GetAsync(Url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(SeedData.UserB, 2).GetAsync(Url)).StatusCode);
        }

        [Fact]
        public async Task Not_configured_runtime_returns_503()
        {
            using var disabled = new RagApiFactory();
            await SeedData.ResetAsync(disabled);
            var client = disabled.CreateClientWithToken(TestTokens.Create(disabled.SecretKey, subject: SeedData.UserA.ToString()));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Url)).StatusCode);
        }

        private sealed record Artifact(string Id, string Version, string Source, string Content, int TokenEstimate);

        private sealed record Bundle(string SpaceKey, List<Artifact> Skills, Artifact? Workflow, List<string> OmittedSkillIds, int TokenEstimate, int TokenBudget);
    }
}
