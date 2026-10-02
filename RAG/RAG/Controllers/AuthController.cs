using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.Dtos.Auth;
using RAG.Models.Params.Auth;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Auth;
using RAG.Services.Interfaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("驗證功能模組")]
    public class AuthController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;

        public AuthController(
            IMapper mapper,
            IAuthService authService
        )
        {
            _mapper = mapper;
            _authService = authService;
        }

        /// <summary>
        /// 登入
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginParam request)
        {
            var loginDto = _mapper.Map<LoginDto>(request);
            var userInfo = await _authService.LoginAsync(loginDto);

            return Ok(_mapper.Map<LoginVM>(userInfo));
        }

        /// <summary>
        /// 以 RefreshToken 換發新的 AccessToken / RefreshToken
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshParam request)
        {
            var session = await _authService.RefreshAsync(request.RefreshToken);

            return Ok(_mapper.Map<LoginVM>(session));
        }
    }
}
