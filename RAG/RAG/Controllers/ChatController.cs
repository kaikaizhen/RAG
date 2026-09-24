using AutoMapper;
using Library.Http;
using Library.Http.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Params.Chat;
using RAG.Models.ViewModels.Chat;
using RAG.Services.Interfaces;
using System;

namespace RAG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Tags("回應功能模組")]
    public class ChatController : ControllerBase
    {
        private readonly IApiExecutorHandler _apiExecutorHandler;
        private readonly IMapper _mapper;
        private readonly IChatService _chatService;


        public ChatController(
            IApiExecutorHandler apiExecutorHandler,
            IMapper mapper,
            IChatService chatService
        )
        { 
            _apiExecutorHandler = apiExecutorHandler;
            _mapper = mapper;
            _chatService = chatService;
        }

        /// <summary>
        /// 回應
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] SendChatParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<ChatDto>(request);
                var chat = await _chatService.SendChatAsync(dto);

                return _mapper.Map<ChatVM>(chat);
            },
            verify:false,
            parameter:request
            );
        }

        [HttpPost(nameof(ChatStream))]
        public async Task ChatStream([FromBody] SendChatParam request)
        {
            var dto = _mapper.Map<ChatDto>(request);
            ChatReponseDto chat;
            try
            {
                chat = await _chatService.SendChatAsync(dto);
            }
            catch (HttpException ex)
            {
                Response.StatusCode = (int)ex.StatusCode;
                await Response.WriteAsJsonAsync(new { error = ex.Message });
                return;
            }

            var answer = chat.Answer;
            var random = new Random();

            foreach (var ch in answer)
            {
                await Response.WriteAsync($"data: {ch}\n\n");
                await Response.Body.FlushAsync();
                await Task.Delay(random.Next(15, 50));
            }
            await Response.Body.FlushAsync();
        }

        /// <summary>
        /// 查詢目前使用者的聊天室清單
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetChatHistory()
        {
            return await _apiExecutorHandler.RunAsync(async () =>
            {
                var chatHistorys = await _chatService.GetChatHistoryAsync();

                return _mapper.Map<List<GetChatHistoryVM>>(chatHistorys);
            },
            verify: false
            );
        }

        /// <summary>
        /// 查詢聊天室紀錄
        /// </summary>
        /// <param name="chatId"></param>
        /// <returns></returns>
        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChatLogHistory([FromRoute] string chatId)
        {
            return await _apiExecutorHandler.RunAsync(async (chatId) =>
            {
                var chatLogHistorys = await _chatService.GetChatLogHistoryAsync(chatId);

                return _mapper.Map<List<GetChatLogHistoryVM>>(chatLogHistorys);
            },
            verify:false,
            parameter:chatId
            );
        }

        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChatHistory([FromRoute] string chatId)
        {
            return await _apiExecutorHandler.RunAsync(async (chatId) =>
            {
                await _chatService.DeleteChatHistoryAsync(chatId);

                return $"Delete Chat{chatId} Success";
            },
            verify:false,
            parameter:chatId
            );
        }
    }
}
