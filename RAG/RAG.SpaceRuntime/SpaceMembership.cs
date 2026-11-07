namespace RAG.SpaceRuntime
{
    /// <summary>
    /// 帳號在某個 Space 的有效 membership（store 僅回傳 membership 與 Space 皆為啟用者）
    /// </summary>
    public sealed record SpaceMembership(int SpaceId, string SpaceKey, string SpaceName, string Role, bool IsDefault);

    public interface ISpaceMembershipStore
    {
        /// <summary>
        /// 取得帳號所有「membership 啟用且 Space 啟用」的 membership
        /// </summary>
        Task<IReadOnlyList<SpaceMembership>> GetActiveMembershipsAsync(int userId, CancellationToken cancellationToken = default);
    }
}
