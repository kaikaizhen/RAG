using System.Text.Json;

namespace RAG.Tests.SkillRuntime
{
    /// <summary>
    /// 在暫存目錄建立 skills/、workflows/、spaces.json 的受控根目錄
    /// </summary>
    public sealed class SkillFixture : IDisposable
    {
        public SkillFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"rag-skill-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(Root, "skills"));
            Directory.CreateDirectory(Path.Combine(Root, "workflows"));
        }

        public string Root { get; }

        public SkillFixture Skill(string id, string content = "# skill", string? extends = null, string? entry = null, string? manifestId = null, string version = "1.0.0")
        {
            var dir = Path.Combine(Root, "skills", id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
            WriteJson(Path.Combine(dir, "manifest.json"), new { schemaVersion = 1, id = manifestId ?? id, version, name = $"Skill {id}", entry = entry ?? "SKILL.md", extends });
            return this;
        }

        public SkillFixture Workflow(string id, string content = "# workflow", string version = "1.0.0")
        {
            var dir = Path.Combine(Root, "workflows", id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "workflow.md"), content);
            WriteJson(Path.Combine(dir, "manifest.json"), new { schemaVersion = 1, id, version, name = $"Workflow {id}", entry = "workflow.md" });
            return this;
        }

        public SkillFixture Spaces(string[] defaultSkills, string? defaultWorkflow, Dictionary<string, (string[] Skills, string? Workflow)>? spaces = null)
        {
            WriteJson(Path.Combine(Root, "spaces.json"), new
            {
                schemaVersion = 1,
                @default = new { skills = defaultSkills, workflow = defaultWorkflow },
                spaces = (spaces ?? new()).ToDictionary(kv => kv.Key, kv => new { skills = kv.Value.Skills, workflow = kv.Value.Workflow })
            });
            return this;
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* 暫存目錄清理失敗不影響測試結果 */ }
        }
    }
}
