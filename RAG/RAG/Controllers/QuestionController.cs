using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.Question;
using RAG.Models.Params.Question;
using RAG.Models.ViewModels.Question;
using RAG.Services.Interfaces;
using RAG.Spaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [RequireSpaceContext]
    [Tags("問題模組")]
    public class QuestionController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IQuestionService _questionService;

        public QuestionController(
            IMapper mapper,
            IQuestionService questionService
        )
        {
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
            var dtos = await _questionService.GetAsync(request.Id, request.Type);
            var response = _mapper.Map<List<GetQuestionVM>>(dtos);

            return Ok(response);
        }

        /// <summary>
        /// 新增問題
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateQuestionParam request)
        {
            var dto = _mapper.Map<CreateQuestionDto>(request);
            await _questionService.CreateAsync(dto);

            return Ok(new { State = "Success", Message = "Ceate Question Success" });
        }

        /// <summary>
        /// 更新問題
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateQuestionParam request)
        {
            var dto = _mapper.Map<UpdateQuestionDto>(request);
            await _questionService.UpdateAsync(dto);

            return Ok(new { State = "Success", Message = $"Update Question{request.ID} Success" });
        }

        /// <summary>
        /// 刪除問題
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            await _questionService.DeleteAsync(Id);

            return Ok(new { State = "Success", Message = $"Delete Question{Id} Success" });
        }
    }
}
