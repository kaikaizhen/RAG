using AutoMapper;
using Library.Http;
using Newtonsoft.Json;
using RAG.Authentication;
using RAG.Commons.Options;
using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Entities;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;
using System.Text;

namespace RAG.Services
{
    public class ChatService : IChatService
    {
        private readonly IMapper _mapper;
        private readonly RagOption _ragOption;
        private readonly IChatRepo _chatRepo;
        private readonly IChatLogRepo _chatLogRepo;
        private readonly ICurrentUserAccessor _currentUser;

        public ChatService(
            IMapper mapper,
            RagOption ragOption,
            IChatRepo chatRepo,
            IChatLogRepo chatLogRepo,
            ICurrentUserAccessor currentUser
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
            _chatRepo = chatRepo;
            _chatLogRepo = chatLogRepo;
            _currentUser = currentUser;
        }

        public async Task<ChatReponseDto> SendChatAsync(ChatDto dto)
        {
            var userId = _currentUser.UserId;

            // 先驗證既有聊天室的擁有者，未通過前不呼叫 RAG Server
            ChatHeader? existingChat = null;
            if (!string.IsNullOrEmpty(dto.ChatID) && int.TryParse(dto.ChatID, out int chatId))
            {
                existingChat = await _chatRepo.GetByIdAsync(chatId, userId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");
            }

            var sendContent = _mapper.Map<SendChatDto>(dto);
            var url = $"{_ragOption.BaseUrl}{_ragOption.AskUrl}";
            var httpClient = new HttpClient();
            var json = JsonConvert.SerializeObject(sendContent);
            var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, jsonContent);

            if (!response.IsSuccessStatusCode) throw new HttpException((HttpStatusCode)500, "Ask API Error");

            var responseString = await response.Content.ReadAsStringAsync();

            if (responseString is null) throw new Exception("Extension Error");

            var chat = JsonConvert.DeserializeObject<ChatReponseDto>(responseString);

            if (chat is null) throw new Exception("Extension Error");

            ChatHeader chatEntity;
            if (existingChat is null)
            {
                chatEntity = new ChatHeader()
                {
                    UserID = userId,
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
            var entities = await _chatRepo.GetByUserIdAsync(_currentUser.UserId);

            var dtos = _mapper.Map<List<GetChatHistoryDto>>(entities);

            return dtos;
        }

        public async Task<List<GetChatLogHistoryDto>> GetChatLogHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            var userId = _currentUser.UserId;
            _ = await _chatRepo.GetByIdAsync(chatId, userId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");

            var entities = await _chatLogRepo.GetAsync(chatId, userId);

            var dtos = _mapper.Map<List<GetChatLogHistoryDto>>(entities);

            return dtos;
        }

        public async Task DeleteChatHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            var userId = _currentUser.UserId;
            var chat = await _chatRepo.GetByIdAsync(chatId, userId) ?? throw new HttpException(HttpStatusCode.NotFound, "ChatID Not Found");

            // 失敗時不 commit，交易隨 DbContext 釋放而 rollback，並將例外交由上層回應錯誤
            await _chatRepo.TransactionAsync();

            var chatLogs = await _chatLogRepo.GetAsync(chatId, userId);
            if (chatLogs.Count > 0) await _chatLogRepo.DeleteRangeAsync(chatLogs);

            await _chatRepo.DeleteAsync(chat);

            await _chatRepo.CommitAsync();
        }
    }
}
