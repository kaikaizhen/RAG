namespace RAG.Models.Params.Auth
{
    public class LoginParam
    {
        /// <summary>
        /// 帳號
        /// </summary>
        public required string Account { get; set; }
         
        /// <summary>
        /// 密碼
        /// </summary>
        public required string Password { get; set; }
    }
}
