namespace RAG.Models.Dtos.User
{
    public class UpdateUserDto
    {
        /// <summary>
        /// 使用者唯一值
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 帳號
        /// </summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>
        /// 密碼
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 名稱(中文)
        /// </summary>
        public string NameZh { get; set; } =string.Empty;

        /// <summary>
        /// 名稱(英文)
        /// </summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// 名稱(其他語系)
        /// </summary>
        public string NameOther { get; set; } = string.Empty;

        /// <summary>
        /// 管理層級
        /// </summary>
        public string Manage { get; set; } = "free";

        /// <summary>
        /// 帳號啟用狀態
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
