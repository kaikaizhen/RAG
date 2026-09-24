using System.Security.Claims;
using TD_WebApiHelper.Core.Helpers;
using TD_WebApiHelper.Models.JwtToken;

namespace RAG.Authentication
{
    /// <summary>
    /// 取代 Library 的 JwtTokenHelper 註冊：
    /// Library 版本在建構時若找不到 secret 會自動產生並寫檔，且 ApiExecutorHandler 每個 request 都會建構它。
    /// 驗證與簽發已改由原生 JwtBearer / <see cref="IJwtTokenIssuer"/> 負責，因此任何呼叫皆 fail-closed。
    /// </summary>
    public sealed class DisabledLibraryJwtTokenHelper : IJwtTokenHelper
    {
        private const string Message = "Library JWT helper is disabled; use ASP.NET Core JwtBearer authentication and IJwtTokenIssuer.";

        public string GenerateToken<T>(JwtTokenMetaData<T> payload) where T : class => throw new NotSupportedException(Message);

        public ClaimsPrincipal? ValidateToken(string token) => throw new NotSupportedException(Message);

        public string RefreshToken(string refreshToken, TimeSpan accessTokenExpiration, string? guid = null) => throw new NotSupportedException(Message);

        public (string AccessToken, string RefreshToken) GenerateTokens<T>(GeneratedJwtTokens<T> payloads) where T : class => throw new NotSupportedException(Message);
    }
}
