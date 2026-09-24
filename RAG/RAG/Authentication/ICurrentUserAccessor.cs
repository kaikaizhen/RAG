namespace RAG.Authentication
{
    /// <summary>
    /// 取得目前已驗證使用者（唯一來源為 authenticated principal，不接受 request body / query 的 UserID）
    /// </summary>
    public interface ICurrentUserAccessor
    {
        /// <summary>
        /// 目前使用者編號（UserCache.ID）；未驗證或 claim 無效時丟出 401（fail-closed）
        /// </summary>
        int UserId { get; }
    }
}
