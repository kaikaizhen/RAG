using Library.Http;
using RAG.Models.Entities;
using RAG.Rag;
using RAG.Repositories.Interfaces;
using RAG.SpaceRuntime;
using System.Globalization;
using System.Net;

namespace RAG.Context
{
    public static class KnowledgeSearchStatus
    {
        /// <summary>
        /// 至少一筆 Verified 證據
        /// </summary>
        public const string Grounded = "grounded";

        /// <summary>
        /// 只有 Stale / Inferred 證據：可參考但不可視為最終依據
        /// </summary>
        public const string Partial = "partial";

        /// <summary>
        /// 沒有證據：由使用者自己的 AI 處理，不可把 similar 當依據
        /// </summary>
        public const string Insufficient = "insufficient";
    }

    public sealed record KnowledgeItem(int QuestionId, string Question, string Answer, double Score, string Status);

    public sealed record KnowledgeSearchResult(int SpaceId, string SpaceKey, string Status, IReadOnlyList<KnowledgeItem> Evidence, IReadOnlyList<KnowledgeItem> Similar);

    public interface IKnowledgeSearchService
    {
        Task<KnowledgeSearchResult> SearchAsync(SpaceExecutionContext context, string query, string? type, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 只檢索、不生成：RAG Server 取候選 → 以 MySQL（權威）回查同 Space 的內容與狀態 → 依門檻分為證據與類似答案
    /// </summary>
    public class KnowledgeSearchService : IKnowledgeSearchService
    {
        private readonly IRagGateway _ragGateway;
        private readonly IRagScopeFactory _ragScopeFactory;
        private readonly IQuestionsRepo _questionsRepo;
        private readonly ContextRuntimeOptions _options;
        private readonly ILogger<KnowledgeSearchService> _logger;

        public KnowledgeSearchService(IRagGateway ragGateway, IRagScopeFactory ragScopeFactory, IQuestionsRepo questionsRepo, ContextRuntimeOptions options, ILogger<KnowledgeSearchService> logger)
        {
            _ragGateway = ragGateway;
            _ragScopeFactory = ragScopeFactory;
            _questionsRepo = questionsRepo;
            _options = options;
            _logger = logger;
        }

        public async Task<KnowledgeSearchResult> SearchAsync(SpaceExecutionContext context, string query, string? type, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!context.Space.HasPermission(SpacePermissions.KnowledgeRead)) throw new HttpException(HttpStatusCode.Forbidden, "Forbidden");
            if (string.IsNullOrWhiteSpace(query) || query.Length > 2000) throw new HttpException(HttpStatusCode.BadRequest, "query must be 1-2000 characters");

            var spaceId = context.Space.SpaceId;
            var hits = await _ragGateway.SearchAsync(_ragScopeFactory.Create(context), query, _options.SearchLimit, type, cancellationToken);

            var candidates = hits
                .Select(h => (Hit: h, Id: int.TryParse(h.ReferenceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0))
                .Where(x => x.Id > 0)
                .ToList();
            var records = (await _questionsRepo.GetByIdsInSpaceAsync(candidates.Select(c => c.Id), spaceId)).ToDictionary(r => r.ID);

            // 孤兒向量、其他 Space 的紀錄、Superseded 一律不回傳；內容以 MySQL 為準
            var seen = new HashSet<int>();
            var eligible = candidates
                .OrderByDescending(c => c.Hit.Score)
                .Where(c => seen.Add(c.Id) && records.TryGetValue(c.Id, out var r) && r.Status != KnowledgeStatus.Superseded)
                .Select(c => (c.Hit, Record: records[c.Id]))
                .ToList();
            var topScore = eligible.Count > 0 ? eligible[0].Hit.Score : 0;

            var evidence = new List<KnowledgeItem>();
            var similar = new List<KnowledgeItem>();
            foreach (var (hit, record) in eligible)
            {
                var item = new KnowledgeItem(record.ID, record.Question ?? string.Empty, record.Answer ?? string.Empty, hit.Score, record.Status);
                if (hit.Score >= _options.EvidenceScoreThreshold && hit.Score >= topScore - _options.EvidenceRelativeMargin)
                {
                    if (evidence.Count < _options.MaxEvidence) evidence.Add(item);
                }
                else if (hit.Score >= _options.SimilarScoreThreshold && similar.Count < _options.MaxSimilar)
                {
                    similar.Add(item);
                }
            }

            var status = evidence.Any(e => e.Status == KnowledgeStatus.Verified) ? KnowledgeSearchStatus.Grounded
                : evidence.Count > 0 ? KnowledgeSearchStatus.Partial
                : KnowledgeSearchStatus.Insufficient;

            // 稽核只記錄識別與統計，不記錄查詢內容與知識原文
            _logger.LogInformation("ContextAudit knowledge.search user={UserId} space={SpaceId} status={Status} evidence={EvidenceIds} similar={SimilarIds} candidates={Candidates}",
                context.Space.UserId, spaceId, status, string.Join(',', evidence.Select(e => e.QuestionId)), string.Join(',', similar.Select(s => s.QuestionId)), hits.Count);

            return new KnowledgeSearchResult(spaceId, context.Space.SpaceKey, status, evidence, similar);
        }
    }
}
