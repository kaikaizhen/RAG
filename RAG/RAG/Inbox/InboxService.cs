using Library.Http;
using Microsoft.EntityFrameworkCore;
using RAG.Models.Dtos.Question;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using RAG.Webhooks;
using RAG.SpaceRuntime;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Inbox
{
    public interface IInboxService
    {
        Task<KnowledgeDraft> CreateAsync(string source, string question, string answer, int? qaItemTypeId, string? sourceRef, CancellationToken cancellationToken = default);

        Task<List<KnowledgeDraft>> ListAsync(string status, CancellationToken cancellationToken = default);

        Task<KnowledgeDraft> UpdateAsync(int id, string question, string answer, int? qaItemTypeId, CancellationToken cancellationToken = default);

        /// <summary>接受草稿：此時才成為正式知識（寫入資料庫並產生向量）</summary>
        Task<KnowledgeDraft> AcceptAsync(int id, int? qaItemTypeId, string? status, CancellationToken cancellationToken = default);

        Task<KnowledgeDraft> RejectAsync(int id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 暫存區：草稿屬於建立者與其 Space，只有本人能看、能審查。建立草稿也需要該 Space 的 knowledge.write，
    /// 這樣唯讀成員不能拿它灌東西；接受時再由 QuestionService 檢查一次並真正寫入。
    /// </summary>
    public class InboxService : IInboxService
    {
        public const int MaxPendingPerSpace = 200;
        public const int ListLimit = 200;
        public const string DefaultAcceptedStatus = KnowledgeStatus.Inferred;

        /// <summary>處理中（Accepting）超過這段時間視為卡住（例如行程中斷），允許重新認領</summary>
        private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(2);

        private readonly WenDaHubContext _db;
        private readonly ISpaceContextAccessor _spaceContext;
        private readonly IQuestionService _questions;
        private readonly IWebhookPublisher _webhooks;
        private readonly TimeProvider _time;

        public InboxService(WenDaHubContext db, ISpaceContextAccessor spaceContext, IQuestionService questions, IWebhookPublisher webhooks, TimeProvider time)
        {
            _webhooks = webhooks;
            _db = db;
            _spaceContext = spaceContext;
            _questions = questions;
            _time = time;
        }

        public async Task<KnowledgeDraft> CreateAsync(string source, string question, string answer, int? qaItemTypeId, string? sourceRef, CancellationToken cancellationToken = default)
        {
            var space = RequireWrite();
            question = question.Trim();
            answer = answer.Trim();
            if (question.Length == 0 || answer.Length == 0) throw new HttpException(HttpStatusCode.BadRequest, "問題與答案都必填");
            await EnsureQaItemTypeAsync(qaItemTypeId, cancellationToken);

            var hash = HashContent(question, answer);
            var existing = await _db.KnowledgeDraft.FirstOrDefaultAsync(d =>
                d.SpaceID == space.SpaceId && d.UserID == space.UserId && d.ContentHash == hash && d.Status == DraftStatus.Pending, cancellationToken);
            if (existing is not null) return existing; // 同一份內容重送：回傳原本那筆，不重複堆積

            var pending = await _db.KnowledgeDraft.CountAsync(d => d.SpaceID == space.SpaceId && d.UserID == space.UserId && d.Status == DraftStatus.Pending, cancellationToken);
            if (pending >= MaxPendingPerSpace)
            {
                throw new HttpException((HttpStatusCode)429, $"暫存區已有 {MaxPendingPerSpace} 筆待審，請先審查或拒絕一些再送");
            }

            var draft = new KnowledgeDraft
            {
                SpaceID = space.SpaceId,
                UserID = space.UserId,
                Question = question,
                Answer = answer,
                QaItemTypeID = qaItemTypeId,
                Source = source,
                SourceRef = string.IsNullOrWhiteSpace(sourceRef) ? null : sourceRef.Trim(),
                ContentHash = hash,
                Status = DraftStatus.Pending,
                CreateAt = _time.GetUtcNow().UtcDateTime
            };
            _db.KnowledgeDraft.Add(draft);
            await _db.SaveChangesAsync(cancellationToken);
            await _webhooks.PublishAsync(space.UserId, space.SpaceId, WebhookEvents.DraftCreated, DraftEventData(draft), cancellationToken);
            return draft;
        }

        public async Task<List<KnowledgeDraft>> ListAsync(string status, CancellationToken cancellationToken = default)
        {
            var space = RequireRead();
            if (!DraftStatus.All.Contains(status)) throw new HttpException(HttpStatusCode.BadRequest, "不支援的草稿狀態");

            return await _db.KnowledgeDraft.AsNoTracking()
                .Where(d => d.SpaceID == space.SpaceId && d.UserID == space.UserId && d.Status == status)
                .OrderByDescending(d => d.CreateAt).ThenByDescending(d => d.ID)
                .Take(ListLimit)
                .ToListAsync(cancellationToken);
        }

        public async Task<KnowledgeDraft> UpdateAsync(int id, string question, string answer, int? qaItemTypeId, CancellationToken cancellationToken = default)
        {
            var space = RequireWrite();
            var draft = await GetPendingAsync(id, space, cancellationToken);
            question = question.Trim();
            answer = answer.Trim();
            if (question.Length == 0 || answer.Length == 0) throw new HttpException(HttpStatusCode.BadRequest, "問題與答案都必填");
            await EnsureQaItemTypeAsync(qaItemTypeId, cancellationToken);

            draft.Question = question;
            draft.Answer = answer;
            draft.QaItemTypeID = qaItemTypeId;
            draft.ContentHash = HashContent(question, answer);
            draft.Version = NewVersion();
            await SaveGuardedAsync(cancellationToken);
            return draft;
        }

        public async Task<KnowledgeDraft> AcceptAsync(int id, int? qaItemTypeId, string? status, CancellationToken cancellationToken = default)
        {
            var space = RequireWrite();
            var draft = await GetClaimableAsync(id, space, cancellationToken);

            var typeId = qaItemTypeId ?? draft.QaItemTypeID
                ?? throw new HttpException(HttpStatusCode.BadRequest, "請選擇要寫入的問題種類細項");

            // 先「認領」：把狀態改成 Accepting 並換新版本。連點或重複送出的第二個請求會在這一步失敗（409），
            // 不會走到下面寫入知識庫的步驟，所以同一筆草稿不可能產生兩筆知識。
            await ClaimAsync(draft, cancellationToken);

            int questionId;
            try
            {
                questionId = await _questions.CreateAsync(new CreateQuestionDto
                {
                    QaItemTypeID = typeId,
                    Question = draft.Question,
                    Answer = draft.Answer,
                    Status = status ?? DefaultAcceptedStatus
                });
            }
            catch
            {
                // 寫入失敗：放回待審，使用者可以重試（QuestionService 已自行補償向量與資料列）
                await ReleaseAsync(draft);
                throw;
            }

            draft.Status = DraftStatus.Accepted;
            draft.QaItemTypeID = typeId;
            draft.AcceptedQuestionID = questionId;
            draft.ReviewedAt = _time.GetUtcNow().UtcDateTime;
            draft.Version = NewVersion();
            await _db.SaveChangesAsync(cancellationToken);
            await _webhooks.PublishAsync(space.UserId, space.SpaceId, WebhookEvents.DraftAccepted, DraftEventData(draft), cancellationToken);
            return draft;
        }

        public async Task<KnowledgeDraft> RejectAsync(int id, CancellationToken cancellationToken = default)
        {
            var space = RequireRead();
            var draft = await GetPendingAsync(id, space, cancellationToken);

            draft.Status = DraftStatus.Rejected;
            draft.ReviewedAt = _time.GetUtcNow().UtcDateTime;
            draft.Version = NewVersion();
            await SaveGuardedAsync(cancellationToken);
            await _webhooks.PublishAsync(space.UserId, space.SpaceId, WebhookEvents.DraftRejected, DraftEventData(draft), cancellationToken);
            return draft;
        }

        /// <summary>通知內容只放編號與來源資訊，不放問答本文；對方需要內容時用自己送出的原件對應 sourceRef</summary>
        private static object DraftEventData(KnowledgeDraft draft) => new
        {
            draftID = draft.ID,
            status = draft.Status,
            source = draft.Source,
            sourceRef = draft.SourceRef,
            questionID = draft.AcceptedQuestionID
        };

        private async Task<KnowledgeDraft> GetPendingAsync(int id, SpaceContext space, CancellationToken cancellationToken)
        {
            var draft = await _db.KnowledgeDraft.FirstOrDefaultAsync(d => d.ID == id && d.SpaceID == space.SpaceId && d.UserID == space.UserId, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此草稿");
            if (draft.Status == DraftStatus.Accepting) throw new HttpException(HttpStatusCode.Conflict, "這筆草稿正在處理中，請稍後");
            if (draft.Status != DraftStatus.Pending) throw new HttpException(HttpStatusCode.Conflict, "這筆草稿已經審查過了");
            return draft;
        }

        /// <summary>接受用：待審，或「處理中但已逾時」（前一個請求中斷）的草稿</summary>
        private async Task<KnowledgeDraft> GetClaimableAsync(int id, SpaceContext space, CancellationToken cancellationToken)
        {
            var draft = await _db.KnowledgeDraft.FirstOrDefaultAsync(d => d.ID == id && d.SpaceID == space.SpaceId && d.UserID == space.UserId, cancellationToken)
                ?? throw new HttpException(HttpStatusCode.NotFound, "查無此草稿");

            var stale = draft.Status == DraftStatus.Accepting && draft.ReviewedAt is DateTime claimedAt && _time.GetUtcNow().UtcDateTime - claimedAt > ClaimTimeout;
            if (draft.Status == DraftStatus.Accepting && !stale) throw new HttpException(HttpStatusCode.Conflict, "這筆草稿正在處理中，請稍後");
            if (draft.Status != DraftStatus.Pending && !stale) throw new HttpException(HttpStatusCode.Conflict, "這筆草稿已經審查過了");
            return draft;
        }

        private async Task ClaimAsync(KnowledgeDraft draft, CancellationToken cancellationToken)
        {
            draft.Status = DraftStatus.Accepting;
            draft.ReviewedAt = _time.GetUtcNow().UtcDateTime; // 在 Accepting 時代表認領時間
            draft.Version = NewVersion();
            await SaveGuardedAsync(cancellationToken);
        }

        private async Task ReleaseAsync(KnowledgeDraft draft)
        {
            try
            {
                draft.Status = DraftStatus.Pending;
                draft.ReviewedAt = null;
                draft.Version = NewVersion();
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 放回待審失敗：草稿會停在處理中，逾時後可再次接受
            }
        }

        /// <summary>兩個請求同時改同一筆草稿時，後到的拿到 409，而不是各做一次</summary>
        private async Task SaveGuardedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new HttpException(HttpStatusCode.Conflict, "這筆草稿剛被另一個請求處理了，請重新整理");
            }
        }

        private static string NewVersion() => Guid.NewGuid().ToString("N");

        private async Task EnsureQaItemTypeAsync(int? qaItemTypeId, CancellationToken cancellationToken)
        {
            if (qaItemTypeId is int typeId && !await _db.QaItemType.AnyAsync(i => i.ID == typeId, cancellationToken))
            {
                throw new HttpException(HttpStatusCode.BadRequest, "查無此問題種類細項");
            }
        }

        private SpaceContext RequireRead() => Require(SpacePermissions.KnowledgeRead);

        private SpaceContext RequireWrite() => Require(SpacePermissions.KnowledgeWrite);

        private SpaceContext Require(string permission)
        {
            var space = _spaceContext.Current.Space;
            if (!space.HasPermission(permission)) throw new HttpException(HttpStatusCode.Forbidden, "Forbidden");
            return space;
        }

        private static string HashContent(string question, string answer) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(question + "\n" + answer)));
    }
}
