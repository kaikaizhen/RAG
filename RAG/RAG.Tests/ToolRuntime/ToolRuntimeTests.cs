using RAG.SpaceRuntime;
using RAG.ToolRuntime;
using System.Text.Json;

namespace RAG.Tests.ToolRuntime
{
    public class ToolRuntimeTests
    {
        private sealed class Store : ISpaceMembershipStore
        {
            private readonly SpaceMembership[] _memberships;
            public Store(params SpaceMembership[] memberships) => _memberships = memberships;
            public Task<IReadOnlyList<SpaceMembership>> GetActiveMembershipsAsync(int userId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<SpaceMembership>>(_memberships);
        }

        private sealed class RecordingAudit : IToolAuditSink
        {
            public List<ToolAuditEvent> Events { get; } = new();
            public void Record(ToolAuditEvent auditEvent) => Events.Add(auditEvent);
        }

        private sealed class FakeAdapter : IToolAdapter
        {
            private readonly Func<ToolInvocation, CancellationToken, Task<object?>> _behavior;

            public FakeAdapter(string id, string permission = SpacePermissions.KnowledgeRead, TimeSpan? timeout = null, Func<ToolInvocation, CancellationToken, Task<object?>>? behavior = null)
            {
                Definition = new CapabilityDefinition(id, "fake", CapabilityKind.Read, permission, timeout ?? TimeSpan.FromSeconds(5), new[]
                {
                    new ToolArgument("query", ToolArgumentType.String, Required: true, MaxLength: 10),
                    new ToolArgument("limit", ToolArgumentType.Integer, Required: false),
                });
                _behavior = behavior ?? ((inv, _) => Task.FromResult<object?>($"ok:{inv.Context.Space.SpaceKey}:{inv.Arguments["query"]}"));
            }

            public CapabilityDefinition Definition { get; }
            public List<ToolInvocation> Invocations { get; } = new();

            public Task<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
            {
                Invocations.Add(invocation);
                return _behavior(invocation, cancellationToken);
            }
        }

        private static async Task<SpaceExecutionContext> Context(string spaceKey, string role = SpaceRoles.Member)
        {
            var space = (await new AccountSpaceResolver(new Store(new SpaceMembership(spaceKey.GetHashCode() & 0xFFFF | 1, spaceKey, spaceKey, role, true))).ResolveAsync(7, null)).Context!;
            return new SpaceExecutionContext(space, ResourceScope.Empty);
        }

        private static CapabilityConfig Config(Dictionary<string, List<string>> spaces) => new() { SchemaVersion = 1, Spaces = spaces };

        private static (ToolInvoker Invoker, RecordingAudit Audit, CapabilityResolver Resolver) Build(CapabilityConfig config, params IToolAdapter[] adapters)
        {
            var resolver = new CapabilityResolver(new ToolRegistry(adapters), config);
            var audit = new RecordingAudit();
            return (new ToolInvoker(resolver, audit, TimeProvider.System), audit, resolver);
        }

        private static IReadOnlyDictionary<string, JsonElement> Args(object value) =>
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;

        // ---------- 可見性 ----------

        [Fact]
        public async Task Visible_capabilities_are_space_allow_list_intersect_adapters_and_permission()
        {
            var read = new FakeAdapter("demo.read");
            var write = new FakeAdapter("demo.write", SpacePermissions.KnowledgeWrite);
            var (_, _, resolver) = Build(Config(new() { ["team-a"] = new() { "demo.read", "demo.write" } }), read, write);

            Assert.Equal(new[] { "demo.read", "demo.write" }, resolver.Resolve(await Context("team-a")).Select(c => c.Id));
            Assert.Equal(new[] { "demo.read" }, resolver.Resolve(await Context("team-a", SpaceRoles.Viewer)).Select(c => c.Id));
        }

        [Fact]
        public async Task Space_not_in_config_sees_nothing_and_does_not_inherit_default()
        {
            var (_, _, resolver) = Build(Config(new() { ["default"] = new() { "demo.read" } }), new FakeAdapter("demo.read"));

            Assert.Empty(resolver.Resolve(await Context("team-b")));
        }

        // ---------- 執行：每次重新授權 ----------

        [Fact]
        public async Task Allowed_invocation_succeeds_with_context_of_caller_and_is_audited()
        {
            var adapter = new FakeAdapter("demo.read");
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(new { query = "hello" }), "corr-1");

            Assert.Equal(ToolInvocationStatus.Succeeded, result.Status);
            Assert.Equal("ok:team-a:hello", result.Output);
            var e = Assert.Single(audit.Events);
            Assert.Equal(("corr-1", 7, "demo.read", ToolInvocationStatus.Succeeded), (e.CorrelationId, e.UserId, e.CapabilityId, e.Status));
        }

        [Fact]
        public async Task Space_b_cannot_invoke_space_a_capability_even_directly()
        {
            var adapter = new FakeAdapter("demo.read");
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-b"), "demo.read", Args(new { query = "hello" }), "corr-2");

            Assert.Equal(ToolInvocationStatus.Forbidden, result.Status);
            Assert.Empty(adapter.Invocations);
            Assert.Equal(ToolInvocationStatus.Forbidden, Assert.Single(audit.Events).Status);
        }

        [Fact]
        public async Task Missing_role_permission_is_forbidden()
        {
            var adapter = new FakeAdapter("demo.write", SpacePermissions.KnowledgeWrite);
            var (invoker, _, _) = Build(Config(new() { ["team-a"] = new() { "demo.write" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a", SpaceRoles.Viewer), "demo.write", Args(new { query = "x" }), "c");

            Assert.Equal(ToolInvocationStatus.Forbidden, result.Status);
            Assert.Empty(adapter.Invocations);
        }

        [Theory]
        [InlineData("rm -rf /")]
        [InlineData("demo.read; cat /etc/passwd")]
        [InlineData("../demo.read")]
        [InlineData("unknown.tool")]
        public async Task Arbitrary_or_unknown_capability_never_reaches_an_adapter(string capabilityId)
        {
            var adapter = new FakeAdapter("demo.read");
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a"), capabilityId, Args(new { query = "x" }), "c");

            Assert.Equal(ToolInvocationStatus.Forbidden, result.Status);
            Assert.Empty(adapter.Invocations);
            var recorded = Assert.Single(audit.Events).CapabilityId;
            Assert.True(recorded == "<invalid>" || recorded == "unknown.tool");
        }

        // ---------- 參數 ----------

        public static TheoryData<object> InvalidArguments => new()
        {
            new { },                                  // 缺必要參數
            new { query = "" },                       // 空字串
            new { query = "01234567890" },            // 超過長度
            new { query = 123 },                      // 型別錯誤
            new { query = "ok", limit = "ten" },      // 型別錯誤
            new { query = "ok", command = "rm -rf" }, // 未宣告參數
        };

        [Theory]
        [MemberData(nameof(InvalidArguments))]
        public async Task Invalid_arguments_are_rejected_before_adapter(object args)
        {
            var adapter = new FakeAdapter("demo.read");
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(args), "c");

            Assert.Equal(ToolInvocationStatus.InvalidArguments, result.Status);
            Assert.Empty(adapter.Invocations);
            Assert.Equal(ToolInvocationStatus.InvalidArguments, Assert.Single(audit.Events).Status);
        }

        [Fact]
        public async Task Shell_like_input_is_passed_only_as_data()
        {
            var adapter = new FakeAdapter("demo.read");
            var (invoker, _, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(new { query = "$(whoami)" }), "c");

            Assert.Equal("$(whoami)", Assert.Single(adapter.Invocations).Arguments["query"]);
        }

        // ---------- timeout / 取消 / 失敗 ----------

        [Fact]
        public async Task Slow_adapter_times_out()
        {
            var adapter = new FakeAdapter("demo.read", timeout: TimeSpan.FromMilliseconds(50), behavior: async (_, ct) => { await Task.Delay(5000, ct); return null; });
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(new { query = "x" }), "c");

            Assert.Equal(ToolInvocationStatus.TimedOut, result.Status);
            Assert.Equal(ToolInvocationStatus.TimedOut, Assert.Single(audit.Events).Status);
        }

        [Fact]
        public async Task Caller_cancellation_is_propagated()
        {
            var adapter = new FakeAdapter("demo.read", behavior: async (_, ct) => { await Task.Delay(5000, ct); return null; });
            var (invoker, _, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            var result = await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(new { query = "x" }), "c", cts.Token);

            Assert.Equal(ToolInvocationStatus.Cancelled, result.Status);
        }

        [Fact]
        public async Task Adapter_exception_details_and_argument_values_are_not_leaked()
        {
            var adapter = new FakeAdapter("demo.read", behavior: (_, _) => throw new InvalidOperationException("password=hunter2"));
            var (invoker, audit, _) = Build(Config(new() { ["team-a"] = new() { "demo.read" } }), adapter);

            var result = await invoker.InvokeAsync(await Context("team-a"), "demo.read", Args(new { query = "token-abc" }), "c");

            Assert.Equal(ToolInvocationStatus.Failed, result.Status);
            Assert.DoesNotContain("hunter2", result.Error);
            var serializedAudit = JsonSerializer.Serialize(audit.Events);
            Assert.DoesNotContain("token-abc", serializedAudit);
            Assert.DoesNotContain("hunter2", serializedAudit);
            Assert.Contains("query", serializedAudit);
        }

        // ---------- 設定 / registry ----------

        [Theory]
        [InlineData("{\"schemaVersion\":1,\"spaces\":{\"team-a\":[\"ghost.tool\"]}}", "unknown capability")]
        [InlineData("{\"schemaVersion\":1,\"spaces\":{\"team-a\":[\"rm -rf\"]}}", "not a valid capability id")]
        [InlineData("{\"schemaVersion\":1,\"spaces\":{\"../x\":[\"demo.read\"]}}", "not a valid space key")]
        [InlineData("{\"schemaVersion\":2,\"spaces\":{}}", "unsupported schemaVersion")]
        public void Invalid_capability_config_fails_fast(string json, string expected)
        {
            var path = Path.Combine(Path.GetTempPath(), $"rag-cap-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => CapabilityConfig.Load(path, new[] { "demo.read" }));
                Assert.Contains(expected, ex.Message);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Duplicate_or_invalid_adapter_ids_are_rejected()
        {
            Assert.Throws<InvalidOperationException>(() => new ToolRegistry(new IToolAdapter[] { new FakeAdapter("demo.read"), new FakeAdapter("demo.read") }));
            Assert.Throws<InvalidOperationException>(() => new ToolRegistry(new IToolAdapter[] { new FakeAdapter("Bad Id") }));
        }

        [Fact]
        public void Repository_capability_config_is_valid()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Configuration", "capabilities.json");

            var config = CapabilityConfig.Load(path, new[] { "knowledge.search" });

            Assert.Equal(new[] { "knowledge.search" }, config.Spaces["default"]);
        }
    }
}
