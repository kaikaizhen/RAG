using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RAG.SkillRuntime
{
    /// <summary>
    /// skills/&lt;id&gt;/manifest.json
    /// </summary>
    public sealed class SkillManifest
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; init; }

        /// <summary>
        /// 相對於 Skill 目錄的主檔（預設 SKILL.md）
        /// </summary>
        [JsonPropertyName("entry")] public string Entry { get; init; } = "SKILL.md";

        /// <summary>
        /// 受限繼承：父 Skill ID（先載入父，再載入本身）
        /// </summary>
        [JsonPropertyName("extends")] public string? Extends { get; init; }
    }

    /// <summary>
    /// workflows/&lt;id&gt;/manifest.json
    /// </summary>
    public sealed class WorkflowManifest
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("entry")] public string Entry { get; init; } = "workflow.md";
    }

    /// <summary>
    /// spaces.json：Space 只保存 skill_id / workflow_id，不可包含任何路徑
    /// </summary>
    public sealed class SpaceSkillConfig
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
        [JsonPropertyName("default")] public SpaceSkillSelection Default { get; init; } = new();

        /// <summary>
        /// key = Space.Key
        /// </summary>
        [JsonPropertyName("spaces")] public Dictionary<string, SpaceSkillSelection> Spaces { get; init; } = new();
    }

    public sealed class SpaceSkillSelection
    {
        [JsonPropertyName("skills")] public List<string> Skills { get; init; } = new();
        [JsonPropertyName("workflow")] public string? Workflow { get; init; }
    }

    public static class ManifestRules
    {
        public const int SupportedSchemaVersion = 1;

        // ID 與 Space key 只允許小寫英數與連字號：不可能表達路徑、分隔符號或 ..
        private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);

        public static bool IsValidId(string? value) => value is not null && IdPattern.IsMatch(value);
    }
}
