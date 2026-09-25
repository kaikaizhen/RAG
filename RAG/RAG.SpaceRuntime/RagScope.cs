namespace RAG.SpaceRuntime
{
    /// <summary>
    /// 所有 RAG write / search / retrieve / delete 必須攜帶的範圍；僅能由 <see cref="IRagScopeFactory"/> 建立，caller 不可自行指定 space_id。
    /// </summary>
    public sealed class RagScope
    {
        internal RagScope(int spaceId)
        {
            if (spaceId <= 0) throw new ArgumentOutOfRangeException(nameof(spaceId));
            SpaceId = spaceId;
        }

        public int SpaceId { get; }
    }

    public interface IRagScopeFactory
    {
        /// <summary>
        /// 由已解析的 ExecutionContext 建立 RagScope（一般請求唯一入口）
        /// </summary>
        RagScope Create(SpaceExecutionContext context);

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

        public RagScope ForVerifiedRecord(int recordSpaceId) => new(recordSpaceId);
    }
}
