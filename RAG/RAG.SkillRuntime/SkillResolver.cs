using RAG.SpaceRuntime;

namespace RAG.SkillRuntime
{
    public sealed record ResolvedArtifact(string Id, string Version, string Name, string Source, int TokenEstimate);

    /// <summary>
    /// 某個 Space 的有效 Skill / Workflow（只含 identity、version、來源與 token 估計，不含內容）
    /// </summary>
    public sealed record EffectiveSkillBundle(string SpaceKey, IReadOnlyList<ResolvedArtifact> Skills, ResolvedArtifact? Workflow)
    {
        public int TokenEstimate => Skills.Sum(s => s.TokenEstimate) + (Workflow?.TokenEstimate ?? 0);
    }

    public interface ISkillResolver
    {
        /// <summary>
        /// Skill：Default（含 extends 鏈）在前，Space 專屬擴充在後；Workflow：Space 指定者優先，否則明確使用 Default。
        /// 未設定 Skill Runtime 時回傳 null（呼叫端應 fail-closed）。
        /// </summary>
        EffectiveSkillBundle? Resolve(SpaceExecutionContext context);
    }

    public class SkillResolver : ISkillResolver
    {
        public const string DefaultSource = "default";

        private readonly ISkillRegistry _registry;
        private readonly ITokenEstimator _tokenEstimator;

        public SkillResolver(ISkillRegistry registry, ITokenEstimator tokenEstimator)
        {
            _registry = registry;
            _tokenEstimator = tokenEstimator;
        }

        public EffectiveSkillBundle? Resolve(SpaceExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!_registry.IsConfigured) return null;

            var spaceKey = context.Space.SpaceKey;
            var spaceSource = $"space:{spaceKey}";
            var config = _registry.Config;
            config.Spaces.TryGetValue(spaceKey, out var spaceSelection);

            var skills = new List<ResolvedArtifact>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in config.Default.Skills) AddWithParents(id, DefaultSource, skills, seen);
            foreach (var id in spaceSelection?.Skills ?? new List<string>()) AddWithParents(id, spaceSource, skills, seen);

            ResolvedArtifact? workflow = null;
            var (workflowId, workflowSource) = spaceSelection?.Workflow is { } spaceWorkflow
                ? (spaceWorkflow, spaceSource)
                : (config.Default.Workflow, DefaultSource);
            if (workflowId is not null && _registry.TryGetWorkflow(workflowId, out var registered))
            {
                workflow = new ResolvedArtifact(registered.Manifest.Id, registered.Manifest.Version, registered.Manifest.Name, workflowSource, _tokenEstimator.EstimateBytes(registered.EntryBytes));
            }

            return new EffectiveSkillBundle(spaceKey, skills, workflow);
        }

        private void AddWithParents(string id, string source, List<ResolvedArtifact> skills, HashSet<string> seen)
        {
            // registry 已於啟動時驗證 id 存在且無循環；此處仍防禦性地限制深度
            var chain = new Stack<RegisteredSkill>();
            for (string? current = id; current is not null && chain.Count < 32 && _registry.TryGetSkill(current, out var skill); current = skill.Manifest.Extends)
            {
                chain.Push(skill);
            }

            while (chain.Count > 0)
            {
                var skill = chain.Pop();
                if (!seen.Add(skill.Manifest.Id)) continue;
                skills.Add(new ResolvedArtifact(skill.Manifest.Id, skill.Manifest.Version, skill.Manifest.Name, source, _tokenEstimator.EstimateBytes(skill.EntryBytes)));
            }
        }
    }
}
