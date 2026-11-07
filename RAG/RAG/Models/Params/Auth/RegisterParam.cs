namespace RAG.Models.Params.Auth
{
    public class RegisterParam
    {
        /// <summary>
        /// 登入帳號 = 信箱（驗證信會寄到這裡）
        /// </summary>
        public required string Account { get; set; }

        /// <summary>
        /// 密碼
        /// </summary>
        public required string Password { get; set; }

        /// <summary>
        /// 名稱(中文)
        /// </summary>
        public string? NameZh { get; set; }

        /// <summary>
        /// 名稱(英文)
        /// </summary>
        public string? NameEn { get; set; }

        /// <summary>
        /// 名稱(其他語系)
        /// </summary>
        public string? NameOther { get; set; }
    }
}
