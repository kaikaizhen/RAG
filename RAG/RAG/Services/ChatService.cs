using AutoMapper;
using Library.Http;
using Newtonsoft.Json;
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

        public ChatService(
            IMapper mapper,
            RagOption ragOption,
            IChatRepo chatRepo,
            IChatLogRepo chatLogRepo
        )
        {
            _mapper = mapper;
            _ragOption = ragOption;
            _chatRepo = chatRepo;
            _chatLogRepo = chatLogRepo;
        }

        public async Task<ChatReponseDto> SendChatAsync(ChatDto dto)
        {
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
            if (string.IsNullOrEmpty(dto.ChatID) || !int.TryParse(dto.ChatID, out int chatId))
            {
                chatEntity = new ChatHeader()
                {
                    UserID = dto.UserID,
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
                chatEntity = await _chatRepo.GetByIdAsync(chatId) ?? throw new HttpException((HttpStatusCode)400,"ChatID Not Found");

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

        public async Task<List<GetChatHistoryDto>> GetChatHistoryAsync(GetChatDto dto)
        {
            var entities = await _chatRepo.GetByUserIdAsync(dto.UserID);

            var dtos = _mapper.Map<List<GetChatHistoryDto>>(entities);

            return dtos;
        }

        public async Task<List<GetChatLogHistoryDto>> GetChatLogHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            var entities = await _chatLogRepo.GetAsync(chatId);

            var dtos = _mapper.Map<List<GetChatLogHistoryDto>>(entities);

            return dtos;
        }

        public async Task DeleteChatHistoryAsync(string Id)
        {
            if (!int.TryParse(Id, out var chatId)) throw new HttpException((HttpStatusCode)400, "ChatID Not Found");

            await _chatRepo.TransactionAsync();
            try
            {
                var chat = await _chatRepo.GetByIdAsync(chatId);
                var chatLogs = await _chatLogRepo.GetAsync(chatId);

                if (chatLogs is not null) await _chatLogRepo.DeleteRangeAsync(chatLogs);

                if (chat is not null) await _chatRepo.DeleteAsync(chat);

                await _chatRepo.CommitAsync();
            }
            catch (Exception ex) {
                new HttpException((HttpStatusCode)500, ex.Message);
            }
        }
    }
}
