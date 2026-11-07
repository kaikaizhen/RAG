using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.QaTypeItem;
using RAG.Models.Params.QaTypeItem;
using RAG.Models.ViewModels.QaTypeItem;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Tags("問題種類細項功能模組")]
    public class QaTypeItemController : ControllerBase
    {
        private readonly IMapper _mapper;
        
        private readonly IQaTypeItemService _qaTypeItemService;

        public QaTypeItemController(
            IMapper mapper,
            IQaTypeItemService qaTypeItemService
        )
        {
            _mapper = mapper;
            _qaTypeItemService = qaTypeItemService;
        }

        /// <summary>
        /// 查詢問題種類細項列表(狀態為啟用)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var dtos = await _qaTypeItemService.Get();
            var response = _mapper.Map<List<GetQaTypeItemVM>>(dtos);

            return Ok(response);
        }

        /// <summary>
        /// 新增問題種類細項
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateQaTypeItemParam request)
        {
            var dto = _mapper.Map<CreateQaTypeItemDto>(request);
            await _qaTypeItemService.CreateAsync(dto);

            return Ok(new { State = "Success", Message = "Ceate QaTypeItem Success" });
        }

        /// <summary>
        /// 更新問題種類細項
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateQaTypeItemParam request)
        {
            var dto = _mapper.Map<UpdateQaTypeItemDto>(request);
            await _qaTypeItemService.UpdateAsync(dto);

            return Ok(new { State = "Success", Message = $"Update QaTypeItem{request.ID} Success" });
        }

        /// <summary>
        /// 刪除問題種類細項
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            await _qaTypeItemService.DeleteAsync(Id);

            return Ok(new { State = "Success", Message = $"Delete QaTypeItem{Id} Success" });
        }
    }
}
