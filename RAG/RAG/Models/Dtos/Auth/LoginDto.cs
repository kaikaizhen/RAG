namespace RAG.Models.Dtos.Auth
{
    public class LoginDto
    {
        /// <summary>
        /// 帳號
        /// </summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>
        /// 密碼
        /// </summary>

        public string Password { get; set; } = string.Empty;
    }
}
