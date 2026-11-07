using Asp.Versioning;
using AutoMapper;
using Library.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.Chat;
using RAG.Models.Dtos.RagServer;
using RAG.Models.Params.Chat;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Chat;
using RAG.Services.Interfaces;
using RAG.Streaming;
using RAG.Spaces;
using System;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [RequireSpaceContext]
    [Tags("回應功能模組")]
    public class ChatController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IChatService _chatService;


        public ChatController(
            IMapper mapper,
            IChatService chatService
        )
        { 
            _mapper = mapper;
            _chatService = chatService;
        }

        /// <summary>
        /// 回應
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(typeof(ChatVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Chat([FromBody] SendChatParam request)
        {
            var dto = _mapper.Map<ChatDto>(request);
            var chat = await _chatService.SendChatAsync(dto);

            return Ok(_mapper.Map<ChatVM>(chat));
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

            // 使用者按「停止」或斷線時，RequestAborted 會被觸發，不再繼續送
            var aborted = HttpContext.RequestAborted;
            try
            {
                // 先送出聊天室編號（命名事件），前端據此延續同一個對話
                await Response.WriteAsync($"event: chat\ndata: {chat.ChatId}\n\n", aborted);
                await Response.Body.FlushAsync(aborted);

                // 答案早已算好，這裡只是逐段顯示的效果：以文字元素為單位、段數有上限，答案再長也在約 1.5 秒內送完
                foreach (var chunk in SseText.Chunk(chat.Answer))
                {
                    await Response.WriteAsync(SseText.FormatData(chunk), aborted);
                    await Response.Body.FlushAsync(aborted);
                    await Task.Delay(SseText.DelayMilliseconds, aborted);
                }
            }
            catch (OperationCanceledException)
            {
                // 用戶端中止：正常情況，不需要記錄為錯誤
            }
        }

        /// <summary>
        /// 查詢目前使用者的聊天室清單
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<GetChatHistoryVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChatHistory()
        {
            var chatHistorys = await _chatService.GetChatHistoryAsync();

            return Ok(_mapper.Map<List<GetChatHistoryVM>>(chatHistorys));
        }

        /// <summary>
        /// 查詢聊天室紀錄
        /// </summary>
        /// <param name="chatId"></param>
        /// <returns></returns>
        [HttpGet("{chatId}")]
        [ProducesResponseType(typeof(List<GetChatLogHistoryVM>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChatLogHistory([FromRoute] string chatId)
        {
            var chatLogHistorys = await _chatService.GetChatLogHistoryAsync(chatId);

            return Ok(_mapper.Map<List<GetChatLogHistoryVM>>(chatLogHistorys));
        }

        [HttpDelete("{chatId}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteChatHistory([FromRoute] string chatId)
        {
            await _chatService.DeleteChatHistoryAsync(chatId);

            return Ok($"Delete Chat{chatId} Success");
        }
    }
}
