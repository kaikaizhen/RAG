using RAG.SpaceRuntime;

namespace RAG.Tests.SpaceRuntime
{
    public class AccountSpaceResolverTests
    {
        private sealed class FakeStore : ISpaceMembershipStore
        {
            private readonly IReadOnlyList<SpaceMembership> _memberships;

            public FakeStore(params SpaceMembership[] memberships) => _memberships = memberships;

            public Task<IReadOnlyList<SpaceMembership>> GetActiveMembershipsAsync(int userId, CancellationToken cancellationToken = default) =>
                Task.FromResult(_memberships);
        }

        private static SpaceMembership M(int id, string role = SpaceRoles.Member, bool isDefault = false) =>
            new(id, $"space-{id}", $"Space {id}", role, isDefault);

        private static Task<SpaceResolution> Resolve(int? hint, params SpaceMembership[] memberships) =>
            new AccountSpaceResolver(new FakeStore(memberships)).ResolveAsync(1, hint);

        [Fact]
        public async Task Hint_for_membership_resolves_that_space_with_role_permissions()
        {
            var result = await Resolve(20, M(10, isDefault: true), M(20, SpaceRoles.Viewer));

            Assert.True(result.Succeeded);
            Assert.Equal(20, result.Context!.SpaceId);
            Assert.Equal(1, result.Context.UserId);
            Assert.Equal(SpaceRoles.Viewer, result.Context.Role);
            Assert.True(result.Context.HasPermission(SpacePermissions.KnowledgeRead));
            Assert.False(result.Context.HasPermission(SpacePermissions.KnowledgeWrite));
        }

        [Fact]
        public async Task Hint_for_non_membership_is_forbidden_and_never_downgrades_to_default()
        {
            var result = await Resolve(99, M(10, isDefault: true));

            Assert.False(result.Succeeded);
            Assert.Equal(SpaceResolutionFailure.SelectionForbidden, result.Failure);
        }

        [Fact]
        public async Task Hint_for_membership_with_unknown_role_is_forbidden()
        {
            var result = await Resolve(10, M(10, "superadmin"));

            Assert.Equal(SpaceResolutionFailure.SelectionForbidden, result.Failure);
        }

        [Fact]
        public async Task No_hint_single_membership_is_used()
        {
            var result = await Resolve(null, M(10));

            Assert.Equal(10, result.Context!.SpaceId);
        }

        [Fact]
        public async Task No_hint_multiple_memberships_uses_unique_default()
        {
            var result = await Resolve(null, M(10), M(20, isDefault: true), M(30));

            Assert.Equal(20, result.Context!.SpaceId);
        }

        [Fact]
        public async Task No_hint_multiple_memberships_without_default_requires_selection()
        {
            var result = await Resolve(null, M(10), M(20));

            Assert.Equal(SpaceResolutionFailure.SelectionRequired, result.Failure);
        }

        [Fact]
        public async Task No_hint_multiple_defaults_is_ambiguous_and_requires_selection()
        {
            var result = await Resolve(null, M(10, isDefault: true), M(20, isDefault: true));

            Assert.Equal(SpaceResolutionFailure.SelectionRequired, result.Failure);
        }

        [Fact]
        public async Task No_membership_never_falls_back_to_default_space()
        {
            var result = await Resolve(null);

            Assert.Equal(SpaceResolutionFailure.NoMembership, result.Failure);
        }

        [Fact]
        public async Task Only_membership_with_unknown_role_counts_as_no_membership()
        {
            var result = await Resolve(null, M(10, "superadmin"));

            Assert.Equal(SpaceResolutionFailure.NoMembership, result.Failure);
        }

        [Fact]
        public async Task Invalid_user_id_fails_closed()
        {
            var result = await new AccountSpaceResolver(new FakeStore(M(10))).ResolveAsync(0, null);

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task Resource_scope_is_empty_without_hints_and_rejected_with_any_hint()
        {
            var space = (await Resolve(null, M(10))).Context!;
            var resolver = new ResourceScopeResolver();

            var empty = resolver.Resolve(space, null, null);
            Assert.True(empty.Succeeded);
            Assert.True(empty.Scope!.IsEmpty);

            Assert.False(resolver.Resolve(space, "repo-1", null).Succeeded);
            Assert.False(resolver.Resolve(space, null, "project-1").Succeeded);
        }

        [Fact]
        public async Task Space_context_accessor_fails_closed_and_is_immutable()
        {
            var accessor = new SpaceContextAccessor();
            Assert.Throws<InvalidOperationException>(() => accessor.Current);
            Assert.False(accessor.TryGet(out _));

            var context = new SpaceExecutionContext((await Resolve(null, M(10))).Context!, ResourceScope.Empty);
            accessor.Set(context);

            Assert.Same(context, accessor.Current);
            Assert.Throws<InvalidOperationException>(() => accessor.Set(context));
        }
    }
}
