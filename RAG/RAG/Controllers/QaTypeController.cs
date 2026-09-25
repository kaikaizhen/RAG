using Asp.Versioning;
using AutoMapper;
using Library.Http.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.QaType;
using RAG.Models.Params.QaType;
using RAG.Models.ViewModels.QaType;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Tags("問題種類功能模組")]
    public class QaTypeController : ControllerBase
    {
        private readonly IApiExecutorHandler _apiExecutorHandler;
        private readonly IMapper _mapper;

        private readonly IQaTypeService _qaTypeService;

        public QaTypeController(
            IApiExecutorHandler apiExecutorHandler,
            IMapper mapper,
            IQaTypeService qaTypeService
        )
        {
            _apiExecutorHandler = apiExecutorHandler;
            _mapper = mapper;
            _qaTypeService = qaTypeService;
        }

        /// <summary>
        /// 查詢問題種類列表(狀態為啟用)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return await _apiExecutorHandler.RunAsync(async () =>
            {
                var dtos = await _qaTypeService.GetAsync();
                var response = _mapper.Map<List<GetQaTypeVM>>(dtos);

                return response;
            },
            verify:false
            );
        }

        /// <summary>
        /// 新增問題種類
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateQaTypeParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<CreateQaTypeDto>(request);
                await _qaTypeService.CreateAsync(dto);

                return new { State = "Success", Message = "Ceate QaType Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 更新問題種類
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateQaTypeParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<UpdateQaTypeDto>(request);
                await _qaTypeService.UpdateAsync(dto);

                return new { State = "Success", Message = $"Update QaType{request.ID} Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 刪除問題種類
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            return await _apiExecutorHandler.RunAsync(async (Id) =>
            {
                await _qaTypeService.DeleteAsync(Id);

                return new { State = "Success", Message = $"Delete QaType{Id} Success" };
            },
            verify:false,
            parameter:Id
            );
        }
    }
}
