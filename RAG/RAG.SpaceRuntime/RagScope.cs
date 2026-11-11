namespace RAG.SpaceRuntime
{
    /// <summary>
    /// 所有 RAG write / search / retrieve / delete 必須攜帶的範圍；僅能由 <see cref="IRagScopeFactory"/> 建立，caller 不可自行指定 space_id。
    /// </summary>
    public sealed class RagScope
    {
        internal RagScope(int spaceId) : this(spaceId, new[] { spaceId })
        {
        }

        internal RagScope(int spaceId, IReadOnlyCollection<int> searchSpaceIds)
        {
            if (spaceId <= 0) throw new ArgumentOutOfRangeException(nameof(spaceId));
            if (searchSpaceIds.Count == 0 || searchSpaceIds.Any(id => id <= 0) || !searchSpaceIds.Contains(spaceId))
            {
                throw new ArgumentException("search spaces must be positive and include the primary space", nameof(searchSpaceIds));
            }

            SpaceId = spaceId;
            SearchSpaceIds = searchSpaceIds.Distinct().Order().ToList();
        }

        /// <summary>
        /// 主要的 Space：寫入、刪除一律只作用在這一個
        /// </summary>
        public int SpaceId { get; }

        /// <summary>
        /// 檢索（問答 / 搜尋）涵蓋的 Space，至少包含 <see cref="SpaceId"/>；只影響讀取
        /// </summary>
        public IReadOnlyList<int> SearchSpaceIds { get; }
    }

    public interface IRagScopeFactory
    {
        /// <summary>
        /// 由已解析的 ExecutionContext 建立 RagScope（一般請求唯一入口）
        /// </summary>
        RagScope Create(SpaceExecutionContext context);

        /// <summary>
        /// 讀取用：檢索涵蓋多個 Space。<paramref name="verifiedSpaceIds"/> 必須是伺服器依 membership 與權限算出來的清單
        /// （見 ISearchScopeResolver），不可傳入 client 值；必須包含目前的 Space
        /// </summary>
        RagScope CreateForSearch(SpaceExecutionContext context, IReadOnlyCollection<int> verifiedSpaceIds);

        /// <summary>
        /// 由資料庫中已驗證擁有權的紀錄建立 RagScope（例如刪除本人帳號時清除其各 Space 的向量）。
        /// 僅可傳入從 DB 讀出、且已確認屬於呼叫者的紀錄 SpaceID，不可傳入 client 值。
        /// </summary>
        RagScope ForVerifiedRecord(int recordSpaceId);
    }

    public class RagScopeFactory : IRagScopeFactory
    {
        public RagScope Create(SpaceExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new RagScope(context.Space.SpaceId);
        }

        public RagScope CreateForSearch(SpaceExecutionContext context, IReadOnlyCollection<int> verifiedSpaceIds)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(verifiedSpaceIds);
            return new RagScope(context.Space.SpaceId, verifiedSpaceIds);
        }

        public RagScope ForVerifiedRecord(int recordSpaceId) => new(recordSpaceId);
    }
}
