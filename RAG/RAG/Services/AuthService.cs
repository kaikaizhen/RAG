using AutoMapper;
using Library.Http;
using RAG.Models.Dtos.Auth;
using RAG.Repositories.Interfaces;
using RAG.Services.Interfaces;
using System.Net;
using TD_WebApiHelper.Core.Helpers;
using TD_WebApiHelper.Models.JwtToken;

namespace RAG.Services
{
    public class AuthService: IAuthService
    {
        private readonly IMapper _mapper;

        private readonly IJwtTokenHelper _jwtTokenHelper;

        private readonly IUserCacheRepo _userCacheRepo;

        public AuthService(
            IMapper mapper,
            IJwtTokenHelper jwtTokenHelper,
            IUserCacheRepo userCacheRepo
        )
        {
            _mapper = mapper;
            _jwtTokenHelper = jwtTokenHelper;
            _userCacheRepo = userCacheRepo;
        }

        public async Task<LoginInfoDto> LoginAsync(LoginDto dto)
        {
            var userInfo = await _userCacheRepo.GetUserByAccountAndPasswordAsync(dto.Account, dto.Password) ?? throw new HttpException((HttpStatusCode)401,"登入失敗");

            var tokenInfo = new GeneratedJwtTokens<Dictionary<string, string>>()
            {
                AccessToken = new JwtTokenMetaData<Dictionary<string, string>>()
                {
                    Username = userInfo.ID.ToString(),
                    Expiration = TimeSpan.FromHours(3)
                },
                RefreshToken = new JwtTokenMetaData<Dictionary<string, string>>()
                {
                    Username = userInfo.ID.ToString(),
                    Expiration = TimeSpan.FromHours(8)
                }
            };

            var (accessToken, refreshToken) = _jwtTokenHelper.GenerateTokens(tokenInfo);

            var loginInfo = _mapper.Map<LoginInfoDto>(userInfo, opt => { opt.Items["Language"] = "ZhTw"; });
            loginInfo.AccessToken = accessToken;
            loginInfo.RefreshToken = refreshToken;

            return loginInfo;
        }
    }
}
