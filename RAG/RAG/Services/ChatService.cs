using AutoMapper;
using Library.Http;
using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Rag;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using RAG.SpaceRuntime;
using System.Net;

namespace RAG.Services
{
    public class ChatService : IChatService
    {
        private readonly IMapper _mapper;
        private readonly IChatRepo _chatRepo;
        private readonly IChatLogRepo _chatLogRepo;
        private readonly ISpaceContextAccessor _spaceContext;
        private readonly IRagScopeFactory _ragScopeFactory;
        private readonly IRagGateway _ragGateway;

        public ChatService(
            IMapper mapper,
            IChatRepo chatRepo,
            IChatLogRepo chatLogRepo,
            ISpaceContextAccessor spaceContext,
            IRagScopeFactory ragScopeFactory,
            IRagGateway ragGateway
        )
        {
            _mapper = mapper;
            _chatRepo = chatRepo;
            _chatLogRepo = chatLogRepo;
            _spaceContext = spaceContext;
            _ragScopeFactory = ragScopeFactory;
            _ragGateway = ragGateway;
        }

        public async Task<ChatReponseDto> SendChatAsync(ChatDto dto)
        {
            var context = _spaceContext.Current;
            var userId = context.Space.UserId;
            var spaceId = context.Space.SpaceId;

            if (!context.Space.HasPermission(SpacePermissions.KnowledgeRead)) throw new HttpException(HttpStatusCode.Forbidden, "Forbidden");

            // 先驗證既有聊天室的擁有者與 Space，未通過前不呼叫 RAG Server
            ChatHeader? existingChat = null;
            if (!string.IsNullOrEmpty(dto.ChatID) && int.TryParse(dto.ChatID, out int chatId))
            {
                existingChat = await _chatRepo.GetByIdAsync(chatId, userId, spaceId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");
            }

            // 只搜尋目前 Space 的知識
            var chat = await _ragGateway.AskAsync(_ragScopeFactory.Create(context), dto.Question, dto.Type);

            ChatHeader chatEntity;
            if (existingChat is null)
            {
                chatEntity = new ChatHeader()
                {
                    UserID = userId,
                    SpaceID = spaceId,
                    Name = chat.Related.First(),
                    CreateAt = DateTime.Now
                };
                chatEntity.ChatLog.Add(new ChatLog()
                {
                    Questions = dto.Question,
                    Answer = chat.Answer
                });
                await _chatRepo.AddAsync(chatEntity);
            }
            else
            {
                chatEntity = existingChat;

                chatEntity.UpdateAt = DateTime.Now;
                chatEntity.ChatLog.Add(new ChatLog()
                {
                    Questions = dto.Question,
                    Answer = chat.Answer
                });
                await _chatRepo.UpdateAsync(chatEntity);
            }

            return chat;
        }

        public async Task<List<GetChatHistoryDto>> GetChatHistoryAsync()
        {
            var space = _spaceContext.Current.Space;
            var entities = await _chatRepo.GetByUserIdAsync(space.UserId, space.SpaceId);

            var dtos = _mapper.Map<List<GetChatHistoryDto>>(entities);

            return dtos;
        }

        public async Task<List<GetChatLogHistoryDto>> GetChatLogHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            var space = _spaceContext.Current.Space;
            _ = await _chatRepo.GetByIdAsync(chatId, space.UserId, space.SpaceId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");

            var entities = await _chatLogRepo.GetAsync(chatId, space.UserId, space.SpaceId);

            var dtos = _mapper.Map<List<GetChatLogHistoryDto>>(entities);

            return dtos;
        }

        public async Task DeleteChatHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            var space = _spaceContext.Current.Space;
            var chat = await _chatRepo.GetByIdAsync(chatId, space.UserId, space.SpaceId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");

            // 失敗時不 commit，交易隨 DbContext 釋放而 rollback，並將例外交由上層回應錯誤
            await _chatRepo.TransactionAsync();

            var chatLogs = await _chatLogRepo.GetAsync(chatId, space.UserId, space.SpaceId);
            if (chatLogs.Count > 0) await _chatLogRepo.DeleteRangeAsync(chatLogs);

            await _chatRepo.DeleteAsync(chat);

            await _chatRepo.CommitAsync();
        }
    }
}
