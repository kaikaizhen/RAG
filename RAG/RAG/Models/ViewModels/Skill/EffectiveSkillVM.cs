namespace RAG.Models.ViewModels.Skill
{
    public class EffectiveSkillVM
    {
        /// <summary>
        /// Space 識別字
        /// </summary>
        public string SpaceKey { get; set; } = string.Empty;

        /// <summary>
        /// 有效 Skill（依載入順序：Default 在前、Space 擴充在後）
        /// </summary>
        public List<ArtifactVM> Skills { get; set; } = new();

        /// <summary>
        /// 有效 Workflow（Space 指定或 Default；皆未設定時為 null）
        /// </summary>
        public ArtifactVM? Workflow { get; set; }

        /// <summary>
        /// 預估 token 數
        /// </summary>
        public int TokenEstimate { get; set; }
    }

    public class ArtifactVM
    {
        /// <summary>
        /// 識別字
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 來源：default 或 space:&lt;key&gt;
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 預估 token 數
        /// </summary>
        public int TokenEstimate { get; set; }
    }
}
