using AutoMapper;
using Library.Http.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.User;
using RAG.Models.Params.User;
using RAG.Models.ViewModels.User;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Tags("使用者管理模組")]
    public class UserController : ControllerBase
    {
        private readonly IApiExecutorHandler _apiExecutorHandler;
        private readonly IMapper _mapper;
        private readonly IUserService _userService;

        public UserController(
            IApiExecutorHandler apiExecutorHandler,
            IMapper mapper,
            IUserService userService
        )
        {
            _apiExecutorHandler = apiExecutorHandler;
            _mapper = mapper;
            _userService = userService;
        }

        /// <summary>
        /// 查詢使用者資訊
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        public async Task<IActionResult> Get([FromRoute] int Id)
        {
            return await _apiExecutorHandler.RunAsync(async (Id) =>
            {
                var user = await _userService.GetAsyc(Id);
                var response = _mapper.Map<GetUserVM>(user);

                return response;
            },
            verify:false,
            parameter:Id
            );
        }

        /// <summary>
        /// 新增使用者
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<CreateUserDto>(request);
                await _userService.CreateAsync(dto);

                return new { State = "Success", Message = "Ceate User Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 更新使用者
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateUserParam request)
        {
            return await _apiExecutorHandler.RunAsync(async (request) =>
            {
                var dto = _mapper.Map<UpdateUserDto>(request);
                await _userService.UpdateAsync(dto);

                return new { State = "Success", Message = $"Upate User{request.ID} Success" };
            },
            verify:false,
            parameter:request
            );
        }

        /// <summary>
        /// 刪除使用者
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            return await _apiExecutorHandler.RunAsync(async (Id) =>
            {
                await _userService.DeleteAsync(Id);

                return new { State = "Success", Message = $"Delete User{Id} Success" };
            },
            verify:false,
            parameter:Id
            );
        }
    }
}
