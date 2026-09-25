namespace RAG.Models.Params.Auth
{
    public class RefreshParam
    {
        /// <summary>
        /// 登入時取得的 RefreshToken
        /// </summary>
        public required string RefreshToken { get; set; }
    }
}
