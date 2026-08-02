namespace RAG.Models.Dtos.User
{
    public class CreateUserDto
    {
        /// <summary>
        /// 帳號
        /// </summary>
        public string Account { get; set; } =string.Empty;

        /// <summary>
        /// 密碼
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 名稱(中文)
        /// </summary>
        public string NameZh { get; set; } = string.Empty;

        /// <summary>
        /// 名稱(英文)
        /// </summary>
        public string? NameEn { get; set; } = string.Empty;

        /// <summary>
        /// 名稱(其他語系)
        /// </summary>
        public string NameOther { get; set; } = string.Empty;

        /// <summary>
        /// 管理層級
        /// </summary>
        public string Manage { get; set; } = "free";

        /// <summary>
        /// 帳號啟用狀態(預設：啟用)
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
