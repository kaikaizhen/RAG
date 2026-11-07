using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RAG.Models.Dtos.Auth;
using RAG.Models.Params.Auth;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Auth;
using RAG.Registration;
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
        private readonly IRegistrationService _registrationService;

        public AuthController(
            IMapper mapper,
            IAuthService authService,
            IRegistrationService registrationService
        )
        {
            _mapper = mapper;
            _authService = authService;
            _registrationService = registrationService;
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

        /// <summary>
        /// 自行註冊：帳號為信箱，寄出驗證信。不論信箱是否已註冊，回應一律相同。
        /// 帳號在使用者點擊信中連結（VerifyEmail）後才會建立，並取得自己的個人 Space。
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(RegistrationRateLimits.Register)]
        [ProducesResponseType(typeof(RegistrationResultVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Register([FromBody] RegisterParam request, CancellationToken cancellationToken)
        {
            await _registrationService.RegisterAsync(request.Account, request.Password, request.NameZh, request.NameEn, request.NameOther, cancellationToken);
            return Ok(new RegistrationResultVM { Status = "pending", Message = "若此信箱可以註冊，驗證信已寄出，請到信箱點擊連結完成註冊" });
        }

        /// <summary>
        /// 驗證信箱並啟用帳號（驗證信中的連結）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(RegistrationRateLimits.Verify)]
        [ProducesResponseType(typeof(RegistrationResultVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailParam request, CancellationToken cancellationToken)
        {
            await _registrationService.VerifyEmailAsync(request.Token, cancellationToken);
            return Ok(new RegistrationResultVM { Status = "verified", Message = "信箱已驗證，帳號已啟用，請登入" });
        }
    }
}
