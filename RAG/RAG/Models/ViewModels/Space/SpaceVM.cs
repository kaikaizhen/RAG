namespace RAG.Models.ViewModels.Space
{
    public class SpaceVM
    {
        /// <summary>
        /// Space 編號
        /// </summary>
        public int SpaceID { get; set; }

        /// <summary>
        /// Space 識別字
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Space 名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 是否為預設 Space
        /// </summary>
        public bool IsDefault { get; set; }
    }

    public class CurrentSpaceVM
    {
        /// <summary>
        /// Space 編號
        /// </summary>
        public int SpaceID { get; set; }

        /// <summary>
        /// Space 識別字
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Space 名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 權限
        /// </summary>
        public List<string> Permissions { get; set; } = new();

        /// <summary>
        /// Repository（未選定為 null）
        /// </summary>
        public string? RepositoryID { get; set; }

        /// <summary>
        /// Project（未選定為 null）
        /// </summary>
        public string? ProjectID { get; set; }
    }
}
