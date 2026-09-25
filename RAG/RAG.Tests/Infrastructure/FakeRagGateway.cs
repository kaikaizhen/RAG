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
        public sealed record Call(string Operation, int SpaceId, string? VectorId, ToVectorDto.QuestionRequest? Question, string? AskQuestion);

        public ConcurrentQueue<Call> Calls { get; } = new();

        public bool FailUpsert { get; set; }

        public bool FailDelete { get; set; }

        /// <summary>
        /// SearchAsync 依 space 回傳的命中結果
        /// </summary>
        public ConcurrentDictionary<int, List<RagSearchHit>> SearchHits { get; } = new();

        public void Reset()
        {
            Calls.Clear();
            FailUpsert = false;
            FailDelete = false;
            SearchHits.Clear();
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
            Calls.Enqueue(new Call("ask", scope.SpaceId, null, null, question));
            return Task.FromResult(new ChatReponseDto { Answer = $"answer-from-space-{scope.SpaceId}", Related = new List<string> { "related" } });
        }

        public Task<IReadOnlyList<RagSearchHit>> SearchAsync(RagScope scope, string query, int limit, string? type, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new Call("search", scope.SpaceId, null, null, query));
            IReadOnlyList<RagSearchHit> hits = SearchHits.TryGetValue(scope.SpaceId, out var list) ? list.Take(limit).ToList() : new List<RagSearchHit>();
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
