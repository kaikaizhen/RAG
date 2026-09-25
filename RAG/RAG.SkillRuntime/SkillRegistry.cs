using System.Text.Json;

namespace RAG.SkillRuntime
{
    public sealed class SkillRuntimeOptions
    {
        public const string SectionName = "SkillRuntime";

        /// <summary>
        /// 受控根目錄（含 spaces.json、skills/、workflows/）；未設定時 Skill Runtime 停用
        /// </summary>
        public string? Root { get; set; }

        /// <summary>
        /// 單一內容檔上限（bytes）
        /// </summary>
        public int MaxFileBytes { get; set; } = 256 * 1024;
    }

    public sealed record RegisteredSkill(SkillManifest Manifest, string EntryPath, int EntryBytes);

    public sealed record RegisteredWorkflow(WorkflowManifest Manifest, string EntryPath, int EntryBytes);

    public sealed class SkillRegistryException : Exception
    {
        public SkillRegistryException(IReadOnlyList<string> errors)
            : base("Invalid skill registry:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => " - " + e)))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    public interface ISkillRegistry
    {
        bool IsConfigured { get; }

        SpaceSkillConfig Config { get; }

        bool TryGetSkill(string id, out RegisteredSkill skill);

        bool TryGetWorkflow(string id, out RegisteredWorkflow workflow);
    }

    /// <summary>
    /// 啟動時一次載入並驗證全部 manifest 與 spaces.json（不可變；變更需重新部署 / 重啟以固定版本）
    /// </summary>
    public sealed class SkillRegistry : ISkillRegistry
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private readonly IReadOnlyDictionary<string, RegisteredSkill> _skills;
        private readonly IReadOnlyDictionary<string, RegisteredWorkflow> _workflows;

        private SkillRegistry(bool configured, SpaceSkillConfig config, IReadOnlyDictionary<string, RegisteredSkill> skills, IReadOnlyDictionary<string, RegisteredWorkflow> workflows)
        {
            IsConfigured = configured;
            Config = config;
            _skills = skills;
            _workflows = workflows;
        }

        public static SkillRegistry Disabled { get; } = new(false, new SpaceSkillConfig(), new Dictionary<string, RegisteredSkill>(), new Dictionary<string, RegisteredWorkflow>());

        public bool IsConfigured { get; }

        public SpaceSkillConfig Config { get; }

        public bool TryGetSkill(string id, out RegisteredSkill skill) => _skills.TryGetValue(id, out skill!);

        public bool TryGetWorkflow(string id, out RegisteredWorkflow workflow) => _workflows.TryGetValue(id, out workflow!);

        public static SkillRegistry Load(string root, int maxFileBytes)
        {
            var errors = new List<string>();
            if (!Directory.Exists(root)) throw new SkillRegistryException(new[] { $"root '{root}' does not exist" });

            var skills = LoadManifests<SkillManifest, RegisteredSkill>(Path.Combine(root, "skills"), maxFileBytes, errors,
                m => (m.SchemaVersion, m.Id, m.Version, m.Entry),
                (m, path, bytes) => new RegisteredSkill(m, path, bytes));
            var workflows = LoadManifests<WorkflowManifest, RegisteredWorkflow>(Path.Combine(root, "workflows"), maxFileBytes, errors,
                m => (m.SchemaVersion, m.Id, m.Version, m.Entry),
                (m, path, bytes) => new RegisteredWorkflow(m, path, bytes));

            ValidateExtends(skills, errors);

            var config = LoadConfig(root, errors);
            if (config is not null) ValidateConfig(config, skills, workflows, errors);

            if (errors.Count > 0) throw new SkillRegistryException(errors);

            return new SkillRegistry(true, config!, skills, workflows);
        }

        private static Dictionary<string, TRegistered> LoadManifests<TManifest, TRegistered>(
            string directory, int maxFileBytes, List<string> errors,
            Func<TManifest, (int SchemaVersion, string Id, string Version, string Entry)> describe,
            Func<TManifest, string, int, TRegistered> create)
        {
            var result = new Dictionary<string, TRegistered>(StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return result;

            foreach (var dir in Directory.GetDirectories(directory))
            {
                var dirName = Path.GetFileName(dir);
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                {
                    errors.Add($"{dirName}: directory is a symbolic link");
                    continue;
                }

                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    errors.Add($"{dirName}: manifest.json is missing");
                    continue;
                }

                TManifest? manifest;
                try
                {
                    manifest = JsonSerializer.Deserialize<TManifest>(File.ReadAllText(manifestPath), JsonOptions);
                }
                catch (JsonException ex)
                {
                    errors.Add($"{dirName}: manifest.json is invalid JSON ({ex.Message})");
                    continue;
                }
                if (manifest is null) { errors.Add($"{dirName}: manifest.json is empty"); continue; }

                var (schemaVersion, id, version, entry) = describe(manifest);
                if (schemaVersion != ManifestRules.SupportedSchemaVersion) errors.Add($"{dirName}: unsupported schemaVersion {schemaVersion}");
                if (!ManifestRules.IsValidId(id) || id != dirName) { errors.Add($"{dirName}: id '{id}' must be valid and equal the directory name"); continue; }
                if (string.IsNullOrWhiteSpace(version)) errors.Add($"{id}: version is required");

                if (!ControlledPath.TryResolveFile(dir, entry, out var entryPath, out var pathError))
                {
                    errors.Add($"{id}: entry {pathError}");
                    continue;
                }

                var bytes = (int)Math.Min(int.MaxValue, new FileInfo(entryPath).Length);
                if (bytes > maxFileBytes) { errors.Add($"{id}: entry exceeds {maxFileBytes} bytes"); continue; }

                result[id] = create(manifest, entryPath, bytes);
            }

            return result;
        }

        private static void ValidateExtends(Dictionary<string, RegisteredSkill> skills, List<string> errors)
        {
            foreach (var (id, skill) in skills)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { id };
                for (var parent = skill.Manifest.Extends; parent is not null;)
                {
                    if (!skills.TryGetValue(parent, out var parentSkill)) { errors.Add($"{id}: extends unknown skill '{parent}'"); break; }
                    if (!visited.Add(parent)) { errors.Add($"{id}: inheritance cycle via '{parent}'"); break; }
                    parent = parentSkill.Manifest.Extends;
                }
            }
        }

        private static SpaceSkillConfig? LoadConfig(string root, List<string> errors)
        {
            var path = Path.Combine(root, "spaces.json");
            if (!File.Exists(path)) { errors.Add("spaces.json is missing"); return null; }

            try
            {
                return JsonSerializer.Deserialize<SpaceSkillConfig>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException("empty");
            }
            catch (JsonException ex)
            {
                errors.Add($"spaces.json is invalid JSON ({ex.Message})");
                return null;
            }
        }

        private static void ValidateConfig(SpaceSkillConfig config, Dictionary<string, RegisteredSkill> skills, Dictionary<string, RegisteredWorkflow> workflows, List<string> errors)
        {
            if (config.SchemaVersion != ManifestRules.SupportedSchemaVersion) errors.Add($"spaces.json: unsupported schemaVersion {config.SchemaVersion}");

            void Check(string owner, SpaceSkillSelection selection)
            {
                foreach (var skillId in selection.Skills)
                {
                    if (!ManifestRules.IsValidId(skillId)) errors.Add($"spaces.json {owner}: '{skillId}' is not a valid skill id");
                    else if (!skills.ContainsKey(skillId)) errors.Add($"spaces.json {owner}: unknown skill '{skillId}'");
                }
                if (selection.Workflow is { } workflowId)
                {
                    if (!ManifestRules.IsValidId(workflowId)) errors.Add($"spaces.json {owner}: '{workflowId}' is not a valid workflow id");
                    else if (!workflows.ContainsKey(workflowId)) errors.Add($"spaces.json {owner}: unknown workflow '{workflowId}'");
                }
            }

            Check("default", config.Default);
            foreach (var (spaceKey, selection) in config.Spaces)
            {
                if (!ManifestRules.IsValidId(spaceKey)) errors.Add($"spaces.json: '{spaceKey}' is not a valid space key");
                Check($"space '{spaceKey}'", selection);
            }
        }
    }
}
