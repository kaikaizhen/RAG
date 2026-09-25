using AutoMapper;
using Library.Http;
using RAG.Commons.Enums;
using RAG.Models.Dtos.Question;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Rag;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using RAG.SpaceRuntime;
using System.Net;

namespace RAG.Services
{
    /// <summary>
    /// MySQL 與 Qdrant 不是跨系統 ACID：
    /// 新增 → MySQL（交易內）→ Qdrant → 回寫 VectorID → commit；任一步失敗即補償刪除已建立的向量與資料列。
    /// 更新 → 覆寫同一個 point → 更新 MySQL；MySQL 失敗時重送更新即可收斂。
    /// 刪除 → 先刪 Qdrant（不存在視為成功）→ 再刪 MySQL；中斷後可安全重試。
    /// </summary>
    public class QuestionService : IQuestionService
    {
        private readonly IMapper _mapper;
        private readonly IQuestionsRepo _questionsRepo;
        private readonly IQaTypeItemRepo _qaTypeItemRepo;
        private readonly ISpaceContextAccessor _spaceContext;
        private readonly IRagScopeFactory _ragScopeFactory;
        private readonly IRagGateway _ragGateway;
        private readonly ILogger<QuestionService> _logger;

        public QuestionService(
            IMapper mapper,
            IQuestionsRepo questionsRepo,
            IQaTypeItemRepo qaTypeItemRepo,
            ISpaceContextAccessor spaceContext,
            IRagScopeFactory ragScopeFactory,
            IRagGateway ragGateway,
            ILogger<QuestionService> logger
        )
        {
            _mapper = mapper;
            _questionsRepo = questionsRepo;
            _qaTypeItemRepo = qaTypeItemRepo;
            _spaceContext = spaceContext;
            _ragScopeFactory = ragScopeFactory;
            _ragGateway = ragGateway;
            _logger = logger;
        }

        public async Task<List<GetQuestionDto>> GetAsync(int? Id, QuestionEnum type)
        {
            var space = RequirePermission(SpacePermissions.KnowledgeRead).Space;
            var entitys = type switch
            {
                QuestionEnum.All => await _questionsRepo.Get(space.UserId, space.SpaceId),
                QuestionEnum.Type => await GetByTypeAsync(space, Id),
                _ => throw new HttpException((HttpStatusCode)400,"不支援查詢類型")
            };

            return _mapper.Map<List<GetQuestionDto>>(entitys);
        }

        public async Task CreateAsync(CreateQuestionDto dto)
        {
            var context = RequirePermission(SpacePermissions.KnowledgeWrite);
            var qaTypeId = await GetQaTypeIdAsync(dto.QaItemTypeID);
            var scope = _ragScopeFactory.Create(context);

            var entity = _mapper.Map<Questions>(dto);
            entity.UserID = context.Space.UserId;
            entity.SpaceID = context.Space.SpaceId;

            await _questionsRepo.TransactionAsync();
            await _questionsRepo.AddAsync(entity);

            string? vectorId = null;
            try
            {
                vectorId = await _ragGateway.UpsertQuestionAsync(scope, ToVectorRequest(entity, qaTypeId, referenceId: null));
                entity.VectorID = vectorId;
                await _questionsRepo.UpdateAsync(entity);
                await _questionsRepo.CommitAsync();
            }
            catch
            {
                if (vectorId is not null) await TryCompensateAsync(() => _ragGateway.DeleteQuestionAsync(scope, vectorId), "delete orphan vector");
                await TryCompensateAsync(() => _questionsRepo.DeleteAsync(entity), "remove uncommitted question");
                throw;
            }
        }

        public async Task UpdateAsync(UpdateQuestionDto dto)
        {
            var context = RequirePermission(SpacePermissions.KnowledgeWrite);
            var question = await _questionsRepo.GetByIdAsync(dto.ID, context.Space.UserId, context.Space.SpaceId) ?? throw new HttpException(HttpStatusCode.NotFound,"查無問題");
            var qaTypeId = await GetQaTypeIdAsync(dto.QaItemTypeID);

            var entity = _mapper.Map<Questions>(dto);
            entity.UserID = question.UserID;
            entity.SpaceID = question.SpaceID;
            entity.CreateAt = question.CreateAt;

            // 以新內容覆寫同一個 point（原實作誤用舊內容向量化）
            entity.VectorID = await _ragGateway.UpsertQuestionAsync(_ragScopeFactory.Create(context), ToVectorRequest(entity, qaTypeId, question.VectorID));
            await _questionsRepo.UpdateAsync(entity);
        }

        public async Task DeleteAsync(int Id)
        {
            var context = RequirePermission(SpacePermissions.KnowledgeWrite);
            var question = await _questionsRepo.GetByIdAsync(Id, context.Space.UserId, context.Space.SpaceId) ?? throw new HttpException(HttpStatusCode.NotFound,"查無問題");

            if (!string.IsNullOrEmpty(question.VectorID))
            {
                await _ragGateway.DeleteQuestionAsync(_ragScopeFactory.Create(context), question.VectorID);
            }

            await _questionsRepo.DeleteAsync(question);
        }

        #region Private Methods
        private SpaceExecutionContext RequirePermission(string permission)
        {
            var context = _spaceContext.Current;
            if (!context.Space.HasPermission(permission)) throw new HttpException(HttpStatusCode.Forbidden, "Forbidden");
            return context;
        }

        private async Task<List<Questions>> GetByTypeAsync(SpaceContext space, int? Id)
        {
            if (Id is not int typeId) throw new HttpException((HttpStatusCode)400,"使用種類查詢功能，請帶入Id");

            return await _questionsRepo.GetByTypeIdAsync(space.UserId, space.SpaceId, typeId);
        }

        private async Task<int> GetQaTypeIdAsync(int qaItemTypeId)
        {
            var item = await _qaTypeItemRepo.GetByIdAsync(qaItemTypeId) ?? throw new HttpException(HttpStatusCode.BadRequest, "查無此問題種類細項");
            return item.QaTypeID;
        }

        private static ToVectorDto.QuestionRequest ToVectorRequest(Questions entity, int qaTypeId, string? referenceId) => new()
        {
            Id = entity.ID,
            ReferenceID = referenceId,
            QaTypeId = qaTypeId,
            QaItemTypeId = entity.QaItemTypeID,
            Question = entity.Question ?? string.Empty,
            Answer = entity.Answer ?? string.Empty
        };

        private async Task TryCompensateAsync(Func<Task> action, string description)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                // 補償失敗需人工對帳（MySQL VectorID ↔ Qdrant point）
                _logger.LogError(ex, "Question compensation failed: {Description}", description);
            }
        }
        #endregion
    }
}
