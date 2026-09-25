using Microsoft.IdentityModel.JsonWebTokens;

namespace RAG.Authentication
{
    public interface IRefreshTokenValidator
    {
        /// <summary>
        /// 驗證 refresh token（簽章、issuer、audience、有效期、token_use=refresh、subject）；失敗回傳 null
        /// </summary>
        Task<int?> ValidateAsync(string refreshToken);
    }

    public class RefreshTokenValidator : IRefreshTokenValidator
    {
        private readonly JwtOptions _options;

        public RefreshTokenValidator(JwtOptions options)
        {
            _options = options;
        }

        public async Task<int?> ValidateAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var handler = new JsonWebTokenHandler { MapInboundClaims = true };
            var result = await handler.ValidateTokenAsync(refreshToken, _options.CreateValidationParameters());
            if (!result.IsValid) return null;

            var principal = new System.Security.Claims.ClaimsPrincipal(result.ClaimsIdentity);
            if (principal.FindFirst(JwtOptions.TokenUseClaim)?.Value != JwtOptions.RefreshTokenUse) return null;

            return CurrentUserAccessor.TryGetUserId(principal, out var userId) ? userId : null;
        }
    }
}
