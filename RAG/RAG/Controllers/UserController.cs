using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.User;
using RAG.Models.Params.User;
using RAG.Models.ViewModels.User;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Tags("使用者管理模組")]
    public class UserController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IUserService _userService;

        public UserController(
            IMapper mapper,
            IUserService userService
        )
        {
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
            var user = await _userService.GetAsyc(Id);
            var response = _mapper.Map<GetUserVM>(user);

            return Ok(response);
        }

        /// <summary>
        /// 新增使用者
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserParam request)
        {
            var dto = _mapper.Map<CreateUserDto>(request);
            await _userService.CreateAsync(dto);

            return Ok(new { State = "Success", Message = "Ceate User Success" });
        }

        /// <summary>
        /// 更新使用者
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateUserParam request)
        {
            var dto = _mapper.Map<UpdateUserDto>(request);
            await _userService.UpdateAsync(dto);

            return Ok(new { State = "Success", Message = $"Upate User{request.ID} Success" });
        }

        /// <summary>
        /// 刪除使用者
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{Id}")]
        public async Task<IActionResult> Delete([FromRoute] int Id)
        {
            await _userService.DeleteAsync(Id);

            return Ok(new { State = "Success", Message = $"Delete User{Id} Success" });
        }
    }
}
