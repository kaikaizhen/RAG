using System.Text;

namespace RAG.SkillRuntime
{
    public interface ITokenEstimator
    {
        int Estimate(string text);

        /// <summary>
        /// 由位元組數保守估計（UTF-8 中文約 3 bytes / token）
        /// </summary>
        int EstimateBytes(int utf8Bytes);
    }

    /// <summary>
    /// 近似估計：CJK 字元約 1 token、其他字元約 4 字元 1 token（非精確 tokenizer，只用於預算控制）
    /// </summary>
    public sealed class ApproximateTokenEstimator : ITokenEstimator
    {
        public int Estimate(string text)
        {
            var cjk = 0;
            var other = 0;
            foreach (var ch in text)
            {
                if (ch >= 0x2E80 && ch <= 0x9FFF || ch >= 0xF900 && ch <= 0xFAFF || ch >= 0xFF00 && ch <= 0xFFEF) cjk++;
                else other++;
            }
            return cjk + (other + 3) / 4;
        }

        public int EstimateBytes(int utf8Bytes) => (utf8Bytes + 2) / 3;
    }

    public sealed record LoadedArtifact(string Id, string Version, string Source, string Content, int TokenEstimate);

    public sealed record LoadedSkillBundle(IReadOnlyList<LoadedArtifact> Skills, LoadedArtifact? Workflow, int TokenEstimate, IReadOnlyList<string> OmittedSkillIds);

    public interface ISkillBundleLoader
    {
        /// <summary>
        /// 依 bundle 順序經受控 loader 讀取 entry 內容；Workflow 優先，超過 token 預算的 Skill 不載入並列於 OmittedSkillIds
        /// </summary>
        Task<LoadedSkillBundle> LoadAsync(EffectiveSkillBundle bundle, int tokenBudget, CancellationToken cancellationToken = default);
    }

    public class SkillBundleLoader : ISkillBundleLoader
    {
        private readonly ISkillRegistry _registry;
        private readonly ITokenEstimator _tokenEstimator;

        public SkillBundleLoader(ISkillRegistry registry, ITokenEstimator tokenEstimator)
        {
            _registry = registry;
            _tokenEstimator = tokenEstimator;
        }

        public async Task<LoadedSkillBundle> LoadAsync(EffectiveSkillBundle bundle, int tokenBudget, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            if (tokenBudget <= 0) throw new ArgumentOutOfRangeException(nameof(tokenBudget));

            var remaining = tokenBudget;
            LoadedArtifact? workflow = null;

            if (bundle.Workflow is { } resolvedWorkflow && _registry.TryGetWorkflow(resolvedWorkflow.Id, out var registeredWorkflow))
            {
                var content = await File.ReadAllTextAsync(registeredWorkflow.EntryPath, Encoding.UTF8, cancellationToken);
                var tokens = _tokenEstimator.Estimate(content);
                if (tokens <= remaining)
                {
                    workflow = new LoadedArtifact(resolvedWorkflow.Id, resolvedWorkflow.Version, resolvedWorkflow.Source, content, tokens);
                    remaining -= tokens;
                }
            }

            var skills = new List<LoadedArtifact>();
            var omitted = new List<string>();
            foreach (var resolved in bundle.Skills)
            {
                if (!_registry.TryGetSkill(resolved.Id, out var registered)) { omitted.Add(resolved.Id); continue; }

                var content = await File.ReadAllTextAsync(registered.EntryPath, Encoding.UTF8, cancellationToken);
                var tokens = _tokenEstimator.Estimate(content);
                if (tokens > remaining) { omitted.Add(resolved.Id); continue; }

                skills.Add(new LoadedArtifact(resolved.Id, resolved.Version, resolved.Source, content, tokens));
                remaining -= tokens;
            }

            return new LoadedSkillBundle(skills, workflow, tokenBudget - remaining, omitted);
        }
    }
}
