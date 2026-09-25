using Asp.Versioning;
using AutoMapper;
using Library.Http.Interfaces;
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
        private readonly IApiExecutorHandler _apiExecutor;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;

        public AuthController(
            IApiExecutorHandler apiExecutor,
            IMapper mapper,
            IAuthService authService
        )
        {
            _apiExecutor = apiExecutor;
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
            return await _apiExecutor.RunAsync(async (request) =>
            {
                var loginDto = _mapper.Map<LoginDto>(request);
                var userInfo = await _authService.LoginAsync(loginDto);

                return _mapper.Map<LoginVM>(userInfo);
            },
            verify:false,
            parameter: request
            );
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
            return await _apiExecutor.RunAsync(async (request) =>
            {
                var session = await _authService.RefreshAsync(request.RefreshToken);

                return _mapper.Map<LoginVM>(session);
            },
            verify:false,
            parameter: request
            );
        }
    }
}
