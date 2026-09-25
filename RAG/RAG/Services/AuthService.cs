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

        public AuthService(
            IMapper mapper,
            IJwtTokenIssuer jwtTokenIssuer,
            IUserCacheRepo userCacheRepo,
            IRefreshTokenValidator refreshTokenValidator
        )
        {
            _mapper = mapper;
            _jwtTokenIssuer = jwtTokenIssuer;
            _userCacheRepo = userCacheRepo;
            _refreshTokenValidator = refreshTokenValidator;
        }

        public async Task<LoginInfoDto> LoginAsync(LoginDto dto)
        {
            var userInfo = await _userCacheRepo.GetUserByAccountAndPasswordAsync(dto.Account, dto.Password) ?? throw new HttpException((HttpStatusCode)401,"登入失敗");

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
