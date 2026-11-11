using RAG.Models.Dtos.RagServer;
using RAG.Rag;
using RAG.SpaceRuntime;
using System.Collections.Concurrent;

namespace RAG.Tests.Infrastructure
{
    /// <summary>
    /// 取代真實 RAG Server；記錄每次呼叫使用的 RagScope，並可模擬失敗
    /// </summary>
    public class FakeRagGateway : IRagGateway
    {
        public sealed record Call(string Operation, int SpaceId, string? VectorId, ToVectorDto.QuestionRequest? Question, string? AskQuestion, IReadOnlyList<int>? SearchSpaceIds = null);

        public ConcurrentQueue<Call> Calls { get; } = new();

        public bool FailUpsert { get; set; }

        public bool FailDelete { get; set; }

        /// <summary>
        /// 指定 AskAsync 回傳的答案（測試串流輸出用）；null 時使用預設的 answer-from-space-N
        /// </summary>
        public string? AnswerOverride { get; set; }

        /// <summary>
        /// SearchAsync 依 space 回傳的命中結果
        /// </summary>
        public ConcurrentDictionary<int, List<RagSearchHit>> SearchHits { get; } = new();

        /// <summary>
        /// 不論 scope 為何都會混進搜尋結果的 Space（模擬 RAG Server 回了範圍外的命中，驗證 API 端會擋掉）
        /// </summary>
        public List<int> LeakedSpaceIds { get; } = new();

        public void Reset()
        {
            Calls.Clear();
            FailUpsert = false;
            FailDelete = false;
            AnswerOverride = null;
            SearchHits.Clear();
            LeakedSpaceIds.Clear();
        }

        public Task<string> UpsertQuestionAsync(RagScope scope, ToVectorDto.QuestionRequest question, CancellationToken cancellationToken = default)
        {
            var vectorId = question.ReferenceID ?? $"vec-{question.Id}";
            Calls.Enqueue(new Call("upsert", scope.SpaceId, vectorId, question, null));
            if (FailUpsert) throw new Library.Http.HttpException(System.Net.HttpStatusCode.InternalServerError, "ToVector API Error");
            return Task.FromResult(vectorId);
        }

        public Task<ChatReponseDto> AskAsync(RagScope scope, string question, string? type, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new Call("ask", scope.SpaceId, null, null, question, scope.SearchSpaceIds));
            return Task.FromResult(new ChatReponseDto { Answer = AnswerOverride ?? $"answer-from-space-{scope.SpaceId}", Related = new List<string> { "related" } });
        }

        public Task<IReadOnlyList<RagSearchHit>> SearchAsync(RagScope scope, string query, int limit, string? type, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new Call("search", scope.SpaceId, null, null, query, scope.SearchSpaceIds));
            IReadOnlyList<RagSearchHit> hits = scope.SearchSpaceIds.Concat(LeakedSpaceIds).Distinct()
                .SelectMany(id => SearchHits.TryGetValue(id, out var list) ? list : new List<RagSearchHit>())
                .OrderByDescending(h => h.Score)
                .Take(limit)
                .ToList();
            return Task.FromResult(hits);
        }

        public Task DeleteQuestionAsync(RagScope scope, string vectorId, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new Call("delete", scope.SpaceId, vectorId, null, null));
            if (FailDelete) throw new Library.Http.HttpException(System.Net.HttpStatusCode.InternalServerError, "DeleteVector API Error");
            return Task.CompletedTask;
        }
    }
}
