namespace RAG.Registration
{
    /// <summary>
    /// 自行註冊（匿名 + 信箱驗證）設定。預設關閉（fail-closed）：必須明確啟用，且寄信與連結網址都設定好才會開放。
    /// </summary>
    public sealed class RegistrationOptions
    {
        public const string SectionName = "Registration";

        public bool Enabled { get; set; }

        /// <summary>
        /// 驗證頁網址（前端），例如 https://rag.example.com/verify-email；信中連結為「此網址?token=...」
        /// </summary>
        public string VerifyUrlBase { get; set; } = string.Empty;

        public int TokenLifetimeHours { get; set; } = 24;

        /// <summary>
        /// 同一帳號重寄驗證信的最短間隔，避免被拿來對特定信箱洗信
        /// </summary>
        public int ResendCooldownSeconds { get; set; } = 60;

        public int PasswordMinLength { get; set; } = 8;

        /// <summary>
        /// 驗證信顯示的服務名稱
        /// </summary>
        public string ServiceName { get; set; } = "RAGence";
    }
}
