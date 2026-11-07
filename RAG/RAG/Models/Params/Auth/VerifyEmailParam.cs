namespace RAG.Models.Params.Auth
{
    public class VerifyEmailParam
    {
        /// <summary>
        /// 驗證信中連結附帶的權杖
        /// </summary>
        public required string Token { get; set; }
    }
}
