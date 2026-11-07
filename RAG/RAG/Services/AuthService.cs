using AutoMapper;
using Library.Http;
using RAG.Authentication;
using RAG.Models.Dtos.Auth;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;

namespace RAG.Services
{
    public class AuthService: IAuthService
    {
        private readonly IMapper _mapper;

        private readonly IJwtTokenIssuer _jwtTokenIssuer;

        private readonly IUserCacheRepo _userCacheRepo;

        private readonly IRefreshTokenValidator _refreshTokenValidator;

        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            IMapper mapper,
            IJwtTokenIssuer jwtTokenIssuer,
            IUserCacheRepo userCacheRepo,
            IRefreshTokenValidator refreshTokenValidator,
            IPasswordHasher passwordHasher
        )
        {
            _mapper = mapper;
            _jwtTokenIssuer = jwtTokenIssuer;
            _userCacheRepo = userCacheRepo;
            _refreshTokenValidator = refreshTokenValidator;
            _passwordHasher = passwordHasher;
        }

        public async Task<LoginInfoDto> LoginAsync(LoginDto dto)
        {
            var userInfo = await _userCacheRepo.GetByAccountAsync(dto.Account);

            if (userInfo is null)
            {
                // 帳號不存在也做等量運算，避免以回應時間探測帳號
                _passwordHasher.BurnVerification(dto.Password);
                throw new HttpException((HttpStatusCode)401, "登入失敗");
            }

            if (!_passwordHasher.Verify(dto.Password, userInfo.Password, out var needsUpgrade))
            {
                throw new HttpException((HttpStatusCode)401, "登入失敗");
            }

            if (needsUpgrade)
            {
                // 舊版明文密碼（或過時的雜湊參數）在登入成功時升級為目前的雜湊
                userInfo.Password = _passwordHasher.Hash(dto.Password);
                await _userCacheRepo.UpdateAsync(userInfo);
            }

            return IssueSession(userInfo);
        }

        public async Task<LoginInfoDto> RefreshAsync(string refreshToken)
        {
            var userId = await _refreshTokenValidator.ValidateAsync(refreshToken) ?? throw new HttpException(HttpStatusCode.Unauthorized, "Unauthorized");
            var userInfo = await _userCacheRepo.GetByIdAsync(userId) ?? throw new HttpException(HttpStatusCode.Unauthorized, "Unauthorized");

            return IssueSession(userInfo);
        }

        private LoginInfoDto IssueSession(Models.Entities.UserCache userInfo)
        {
            var (accessToken, refreshToken) = _jwtTokenIssuer.IssueTokens(userInfo.ID);

            var loginInfo = _mapper.Map<LoginInfoDto>(userInfo, opt => { opt.Items["Language"] = "ZhTw"; });
            loginInfo.AccessToken = accessToken;
            loginInfo.RefreshToken = refreshToken;

            return loginInfo;
        }
    }
}
