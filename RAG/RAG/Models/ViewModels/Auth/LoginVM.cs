namespace RAG.Models.ViewModels.Auth
{
    public class LoginVM
    {
        /// <summary>
        /// 使用者編號
        /// </summary>
        public int UserID { get; set; }

        /// <summary>
        /// 名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 短期Token
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 長期Token
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 是否有管理權限
        /// </summary>
        public string Manage { get; set; } = "free";
    }
}
