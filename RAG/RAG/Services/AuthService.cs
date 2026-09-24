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

        public AuthService(
            IMapper mapper,
            IJwtTokenIssuer jwtTokenIssuer,
            IUserCacheRepo userCacheRepo
        )
        {
            _mapper = mapper;
            _jwtTokenIssuer = jwtTokenIssuer;
            _userCacheRepo = userCacheRepo;
        }

        public async Task<LoginInfoDto> LoginAsync(LoginDto dto)
        {
            var userInfo = await _userCacheRepo.GetUserByAccountAndPasswordAsync(dto.Account, dto.Password) ?? throw new HttpException((HttpStatusCode)401,"登入失敗");

            var (accessToken, refreshToken) = _jwtTokenIssuer.IssueTokens(userInfo.ID);

            var loginInfo = _mapper.Map<LoginInfoDto>(userInfo, opt => { opt.Items["Language"] = "ZhTw"; });
            loginInfo.AccessToken = accessToken;
            loginInfo.RefreshToken = refreshToken;

            return loginInfo;
        }
    }
}
