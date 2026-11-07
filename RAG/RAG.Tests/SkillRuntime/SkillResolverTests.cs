using RAG.SkillRuntime;
using RAG.SpaceRuntime;

namespace RAG.Tests.SkillRuntime
{
    public class SkillResolverTests
    {
        private sealed class SingleSpaceStore : ISpaceMembershipStore
        {
            private readonly string _key;
            public SingleSpaceStore(string key) => _key = key;
            public Task<IReadOnlyList<SpaceMembership>> GetActiveMembershipsAsync(int userId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<SpaceMembership>>(new[] { new SpaceMembership(1, _key, _key, SpaceRoles.Member, true) });
        }

        private static async Task<SpaceExecutionContext> ContextFor(string spaceKey)
        {
            var space = (await new AccountSpaceResolver(new SingleSpaceStore(spaceKey)).ResolveAsync(1, null)).Context!;
            return new SpaceExecutionContext(space, ResourceScope.Empty);
        }

        private static SkillFixture Standard() => new SkillFixture()
            .Skill("base", content: "# base")
            .Skill("default-skill", content: "# default", extends: "base")
            .Skill("team-a-skill", content: "# A secret rules")
            .Skill("team-b-skill", content: "# B rules")
            .Workflow("default-flow")
            .Workflow("team-a-flow")
            .Spaces(new[] { "default-skill" }, "default-flow", new()
            {
                ["team-a"] = (new[] { "team-a-skill", "base" }, "team-a-flow"),
                ["team-b"] = (new[] { "team-b-skill" }, null),
            });

        private static SkillResolver Resolver(SkillFixture f) => new(SkillRegistry.Load(f.Root, 256 * 1024), new ApproximateTokenEstimator());

        [Fact]
        public async Task Space_without_config_gets_default_bundle_and_default_workflow()
        {
            using var f = Standard();

            var bundle = Resolver(f).Resolve(await ContextFor("unlisted"))!;

            Assert.Equal(new[] { ("base", "default"), ("default-skill", "default") }, bundle.Skills.Select(s => (s.Id, s.Source)));
            Assert.Equal(("default-flow", "default"), (bundle.Workflow!.Id, bundle.Workflow.Source));
        }

        [Fact]
        public async Task Space_extension_is_appended_after_default_without_duplicates()
        {
            using var f = Standard();

            var bundle = Resolver(f).Resolve(await ContextFor("team-a"))!;

            Assert.Equal(
                new[] { ("base", "default"), ("default-skill", "default"), ("team-a-skill", "space:team-a") },
                bundle.Skills.Select(s => (s.Id, s.Source)));
            Assert.Equal(("team-a-flow", "space:team-a"), (bundle.Workflow!.Id, bundle.Workflow.Source));
            Assert.All(bundle.Skills, s => Assert.Equal("1.0.0", s.Version));
        }

        [Fact]
        public async Task Space_without_workflow_explicitly_falls_back_to_default_workflow()
        {
            using var f = Standard();

            var bundle = Resolver(f).Resolve(await ContextFor("team-b"))!;

            Assert.Equal("default-flow", bundle.Workflow!.Id);
            Assert.Equal("default", bundle.Workflow.Source);
        }

        [Fact]
        public async Task Space_b_never_receives_space_a_skill_or_workflow()
        {
            using var f = Standard();

            var bundle = Resolver(f).Resolve(await ContextFor("team-b"))!;

            Assert.DoesNotContain(bundle.Skills, s => s.Id == "team-a-skill");
            Assert.NotEqual("team-a-flow", bundle.Workflow!.Id);
        }

        [Fact]
        public async Task No_default_workflow_and_no_space_workflow_yields_none()
        {
            using var f = new SkillFixture().Skill("a").Spaces(new[] { "a" }, null);

            var bundle = Resolver(f).Resolve(await ContextFor("x"))!;

            Assert.Null(bundle.Workflow);
        }

        [Fact]
        public async Task Disabled_registry_resolves_to_null()
        {
            var resolver = new SkillResolver(SkillRegistry.Disabled, new ApproximateTokenEstimator());

            Assert.Null(resolver.Resolve(await ContextFor("team-a")));
        }

        [Fact]
        public async Task Bundle_loader_loads_through_registry_in_order_and_respects_token_budget()
        {
            using var f = new SkillFixture()
                .Skill("small", content: "短內容")
                .Skill("large", content: new string('字', 500))
                .Skill("tail", content: "尾端")
                .Workflow("flow", content: "流程")
                .Spaces(new[] { "small", "large", "tail" }, "flow");
            var registry = SkillRegistry.Load(f.Root, 256 * 1024);
            var bundle = new SkillResolver(registry, new ApproximateTokenEstimator()).Resolve(await ContextFor("x"))!;

            var loaded = await new SkillBundleLoader(registry, new ApproximateTokenEstimator()).LoadAsync(bundle, tokenBudget: 100);

            Assert.Equal("流程", loaded.Workflow!.Content);
            Assert.Equal(new[] { "small", "tail" }, loaded.Skills.Select(s => s.Id));
            Assert.Equal(new[] { "large" }, loaded.OmittedSkillIds);
            Assert.True(loaded.TokenEstimate <= 100);
        }

        [Theory]
        [InlineData("abcd", 1)]
        [InlineData("中文字", 3)]
        [InlineData("中文ab", 3)]
        public void Token_estimator_is_deterministic(string text, int expected)
        {
            Assert.Equal(expected, new ApproximateTokenEstimator().Estimate(text));
        }
    }
}
