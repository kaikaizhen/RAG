using Microsoft.AspNetCore.Hosting;
using RAG.Models.Entities;
using RAG.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Tests.SkillRuntime
{
    public sealed class SkillApiFactory : RagApiFactory
    {
        public SkillFixture Skills { get; } = new SkillFixture()
            .Skill("rapid", content: "# default rules")
            .Skill("team-2-skill", content: "# team 2 rules")
            .Workflow("default-flow")
            .Spaces(new[] { "rapid" }, "default-flow", new() { ["team-2"] = (new[] { "team-2-skill" }, null) });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("SkillRuntime:Root", Skills.Root);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) Skills.Dispose();
        }
    }

    public class SkillApiIntegrationTests : IClassFixture<SkillApiFactory>, IAsyncLifetime
    {
        private const string Url = "/api/Skill/effective";
        private readonly SkillApiFactory _factory;

        public SkillApiIntegrationTests(SkillApiFactory factory) => _factory = factory;

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
        public async Task Default_space_gets_default_bundle()
        {
            var body = await ClientFor(SeedData.UserA).GetFromJsonAsync<Effective>(Url);

            Assert.Equal("default", body!.SpaceKey);
            Assert.Equal(new[] { ("rapid", "default") }, body.Skills.Select(s => (s.Id, s.Source)));
            Assert.Equal("default-flow", body.Workflow!.Id);
        }

        [Fact]
        public async Task Space_specific_bundle_extends_default()
        {
            var body = await ClientFor(SeedData.UserA, 2).GetFromJsonAsync<Effective>(Url);

            Assert.Equal(new[] { ("rapid", "default"), ("team-2-skill", "space:team-2") }, body!.Skills.Select(s => (s.Id, s.Source)));
            Assert.Equal(("default-flow", "default"), (body.Workflow!.Id, body.Workflow.Source));
        }

        [Fact]
        public async Task Response_contains_metadata_only_not_content()
        {
            var raw = await ClientFor(SeedData.UserA, 2).GetStringAsync(Url);

            Assert.DoesNotContain("team 2 rules", raw);
            Assert.DoesNotContain(_factory.Skills.Root.Replace("\\", "\\\\"), raw);
        }

        [Fact]
        public async Task Unauthorized_space_selection_is_403_and_unauthenticated_is_401()
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(SeedData.UserB, 2).GetAsync(Url)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClientWithToken(null).GetAsync(Url)).StatusCode);
        }

        [Fact]
        public async Task Not_configured_runtime_returns_503()
        {
            using var disabled = new RagApiFactory();
            await SeedData.ResetAsync(disabled);
            var client = disabled.CreateClientWithToken(TestTokens.Create(disabled.SecretKey, subject: SeedData.UserA.ToString()));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Url)).StatusCode);
        }

        private sealed record Artifact(string Id, string Version, string Name, string Source, int TokenEstimate);

        private sealed record Effective(string SpaceKey, List<Artifact> Skills, Artifact? Workflow, int TokenEstimate);
    }
}
