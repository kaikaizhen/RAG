namespace RAG.Authentication.ApiTokens
{
    /// <summary>
    /// 應用程式權杖能做的事。刻意沒有任何「寫入知識庫」的範圍：權杖只能把內容放進暫存區，
    /// 要不要成為正式知識由使用者本人（登入後）審查決定。
    /// </summary>
    public static class ApiTokenScopes
    {
        /// <summary>檢索知識庫（只讀）</summary>
        public const string KnowledgeSearch = "knowledge.search";

        /// <summary>把問答放進暫存區（不會進向量資料庫）</summary>
        public const string InboxWrite = "inbox.write";

        /// <summary>查詢可使用的 Space</summary>
        public const string SpaceRead = "space.read";

        public static readonly IReadOnlyList<string> All = new[] { KnowledgeSearch, InboxWrite, SpaceRead };

        public static bool IsValid(string? scope) => scope is not null && All.Contains(scope);
    }

    /// <summary>
    /// 可以同時接受登入 token 與應用程式權杖的 endpoint 所用的 policy 名稱；
    /// 沒有掛這些 policy 的 endpoint 一律只接受登入 token（權杖會被當成未登入）。
    /// </summary>
    public static class ApiPolicies
    {
        public const string KnowledgeSearch = "ApiKnowledgeSearch";
        public const string InboxWrite = "ApiInboxWrite";
        public const string SpaceRead = "ApiSpaceRead";
    }

    public static class ApiTokenClaims
    {
        public const string TokenUse = "token_use";
        public const string PersonalAccessToken = "pat";
        public const string Scope = "scope";
        public const string AllowedSpace = "pat_space";
        public const string TokenId = "pat_id";
        public const string TokenName = "pat_name";
    }
}
