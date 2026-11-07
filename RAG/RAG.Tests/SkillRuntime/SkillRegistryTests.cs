using RAG.SkillRuntime;

namespace RAG.Tests.SkillRuntime
{
    public class SkillRegistryTests
    {
        private static SkillRegistryException LoadFails(SkillFixture fixture, int maxBytes = 256 * 1024) =>
            Assert.Throws<SkillRegistryException>(() => SkillRegistry.Load(fixture.Root, maxBytes));

        [Fact]
        public void Valid_registry_loads()
        {
            using var f = new SkillFixture().Skill("base").Skill("child", extends: "base").Workflow("flow").Spaces(new[] { "child" }, "flow");

            var registry = SkillRegistry.Load(f.Root, 1024);

            Assert.True(registry.IsConfigured);
            Assert.True(registry.TryGetSkill("child", out _));
            Assert.True(registry.TryGetWorkflow("flow", out _));
        }

        [Fact]
        public void Missing_root_fails()
        {
            Assert.Throws<SkillRegistryException>(() => SkillRegistry.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"), 1024));
        }

        [Fact]
        public void Missing_manifest_fails()
        {
            using var f = new SkillFixture().Spaces(Array.Empty<string>(), null);
            f.WriteFile("skills/orphan/SKILL.md", "# no manifest");

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("manifest.json is missing"));
        }

        [Fact]
        public void Missing_spaces_config_fails()
        {
            using var f = new SkillFixture().Skill("a");

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("spaces.json is missing"));
        }

        [Fact]
        public void Unknown_skill_or_workflow_id_in_config_fails()
        {
            using var f = new SkillFixture().Skill("a").Spaces(new[] { "a", "ghost" }, "no-such-flow");

            var errors = LoadFails(f).Errors;
            Assert.Contains(errors, e => e.Contains("unknown skill 'ghost'"));
            Assert.Contains(errors, e => e.Contains("unknown workflow 'no-such-flow'"));
        }

        [Theory]
        [InlineData("../../../Windows/win.ini")]
        [InlineData("C:/Windows/win.ini")]
        [InlineData("/etc/passwd")]
        [InlineData("docs/../SKILL.md")]
        public void Config_values_that_look_like_paths_are_rejected(string value)
        {
            using var f = new SkillFixture().Skill("a").Spaces(new[] { value }, null, new() { ["team-a"] = (new[] { "a" }, value) });

            var errors = LoadFails(f).Errors;
            Assert.Contains(errors, e => e.Contains($"'{value}' is not a valid skill id"));
            Assert.Contains(errors, e => e.Contains($"'{value}' is not a valid workflow id"));
        }

        [Fact]
        public void Invalid_space_key_is_rejected()
        {
            using var f = new SkillFixture().Skill("a").Spaces(new[] { "a" }, null, new() { ["../team"] = (new[] { "a" }, null) });

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("not a valid space key"));
        }

        [Theory]
        [InlineData("../other/SKILL.md")]
        [InlineData("..\\..\\spaces.json")]
        [InlineData("C:\\Windows\\win.ini")]
        [InlineData("./SKILL.md")]
        public void Manifest_entry_outside_skill_directory_is_rejected(string entry)
        {
            using var f = new SkillFixture().Skill("other").Skill("evil", entry: entry).Spaces(Array.Empty<string>(), null);

            Assert.Contains(LoadFails(f).Errors, e => e.StartsWith("evil: entry"));
        }

        [Fact]
        public void Manifest_id_must_match_directory()
        {
            using var f = new SkillFixture().Skill("real", manifestId: "impostor").Spaces(Array.Empty<string>(), null);

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("must be valid and equal the directory name"));
        }

        [Fact]
        public void Inheritance_cycle_fails()
        {
            using var f = new SkillFixture().Skill("a", extends: "b").Skill("b", extends: "a").Spaces(new[] { "a" }, null);

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("inheritance cycle"));
        }

        [Fact]
        public void Extending_unknown_skill_fails()
        {
            using var f = new SkillFixture().Skill("a", extends: "ghost").Spaces(new[] { "a" }, null);

            Assert.Contains(LoadFails(f).Errors, e => e.Contains("extends unknown skill 'ghost'"));
        }

        [Fact]
        public void Oversized_entry_fails()
        {
            using var f = new SkillFixture().Skill("big", content: new string('x', 2048)).Spaces(new[] { "big" }, null);

            Assert.Contains(LoadFails(f, maxBytes: 1024).Errors, e => e.Contains("exceeds 1024 bytes"));
        }

        [Fact]
        public void Entry_through_symbolic_link_is_rejected()
        {
            using var f = new SkillFixture().Skill("a").Spaces(new[] { "a" }, null);
            var outside = Path.Combine(Path.GetTempPath(), $"rag-outside-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "secret.md"), "secret");
            try
            {
                var link = Path.Combine(f.Root, "skills", "a", "linked");
                try
                {
                    Directory.CreateSymbolicLink(link, outside);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 無 symlink 權限時改用 Windows 目錄 junction（同為 reparse point，不需管理員權限）
                    Assert.True(OperatingSystem.IsWindows(), "symlink creation failed on non-Windows host");
                    using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{outside}\"") { CreateNoWindow = true, UseShellExecute = false })!;
                    mklink.WaitForExit();
                    Assert.Equal(0, mklink.ExitCode);
                }
                Assert.True(File.Exists(Path.Combine(link, "secret.md")));

                var ok = ControlledPath.TryResolveFile(Path.Combine(f.Root, "skills", "a"), "linked/secret.md", out _, out var error);

                Assert.False(ok);
                Assert.Contains("symbolic link", error);
            }
            finally
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }
}
