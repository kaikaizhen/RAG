using AutoMapper;
using Library.Http.Interfaces;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.Question;
using RAG.Models.Params.Question;
using RAG.Models.ViewModels.Question;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Tags("問題模組")]
    public class QuestionController : ControllerBase
    {
        private readonly IApiExecutorHandler _apiExecutorHandler;
        private readonly IMapper _mapper;
        private readonly IQuestionService _questionService;

        public QuestionController(
            IApiExecutorHandler apiExecutorHandler,
            IMapper mapper,
            IQuestionService questionService
        )
        {
            _apiExecutorHandler = apiExecutorHandler;
            _mapper = mapper;
            _questionService = questionService;
        }

        /// <summary>
        /// 查詢問題列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] GetQuestionParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dtos = await _questionService.GetAsync(request.UserID, request.Id, request.Type);
                var response = _mapper.Map<List<GetQuestionVM>>(dtos);

                return response;
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 新增問題
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateQuestionParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<CreateQuestionDto>(request);
                await _questionService.CreateAsync(dto);

                return new { State = "Success", Message = "Ceate Question Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 更新問題
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateQuestionParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<UpdateQuestionDto>(request);
                await _questionService.UpdateAsync(dto);

                return new { State = "Success", Message = $"Update Question{request.ID} Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 刪除問題
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            return await _apiExecutorHandler.RunAsync(async (Id) =>
            {
                await _questionService.DeleteAsync(Id);

                return new { State = "Success", Message = $"Delete Question{Id} Success" };
            },
            verify:false,
            parameter:Id
            );
        }
    }
}
