using RAG.Models.Dtos.RagServer;
using RAG.SpaceRuntime;

namespace RAG.Rag
{
    /// <summary>
    /// 呼叫 FastAPI RAG Server 的唯一入口：每個呼叫都必須帶 RagScope，並自動附加內部金鑰
    /// </summary>
    public interface IRagGateway
    {
        /// <summary>
        /// 寫入 / 更新問題向量；question.ReferenceID 有值時覆寫同一個 point（僅限同 Space）
        /// </summary>
        /// <returns>向量編號</returns>
        Task<string> UpsertQuestionAsync(RagScope scope, ToVectorDto.QuestionRequest question, CancellationToken cancellationToken = default);

        /// <summary>
        /// 僅在 scope 內搜尋並產生回答
        /// </summary>
        Task<ChatReponseDto> AskAsync(RagScope scope, string question, string? type, CancellationToken cancellationToken = default);

        /// <summary>
        /// 只檢索、不生成：回傳 scope 內的命中結果與相似度分數
        /// </summary>
        Task<IReadOnlyList<RagSearchHit>> SearchAsync(RagScope scope, string query, int limit, string? type, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刪除 scope 內的向量；向量不存在時視為已刪除（可重試）
        /// </summary>
        Task DeleteQuestionAsync(RagScope scope, string vectorId, CancellationToken cancellationToken = default);
    }
}
