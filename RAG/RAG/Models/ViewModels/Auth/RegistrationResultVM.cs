namespace RAG.Models.ViewModels.Auth
{
    public class RegistrationResultVM
    {
        /// <summary>
        /// pending = 已受理（若信箱可用，驗證信已寄出）；verified = 帳號已啟用
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
