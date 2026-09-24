namespace RAG.Authentication
{
    public interface IJwtTokenIssuer
    {
        /// <summary>
        /// 為使用者簽發 access / refresh token（subject 為 <c>ClaimTypes.NameIdentifier</c> = UserCache.ID）
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        (string AccessToken, string RefreshToken) IssueTokens(int userId);
    }
}
