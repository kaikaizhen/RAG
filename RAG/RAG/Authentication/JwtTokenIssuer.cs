using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RAG.Authentication
{
    /// <summary>
    /// 原生 JWT 簽發；claim 結構與既有 Library 簽發的 token 相容（NameIdentifier + jti），
    /// 另加入 token_use 以避免 refresh token 被當作 access token 使用。
    /// </summary>
    public class JwtTokenIssuer : IJwtTokenIssuer
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _timeProvider;

        public JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider)
        {
            _options = options;
            _timeProvider = timeProvider;
        }

        public (string AccessToken, string RefreshToken) IssueTokens(int userId)
        {
            var accessToken = CreateToken(userId, JwtOptions.AccessTokenUse, _options.AccessTokenLifetime);
            var refreshToken = CreateToken(userId, JwtOptions.RefreshTokenUse, _options.RefreshTokenLifetime);

            return (accessToken, refreshToken);
        }

        private string CreateToken(int userId, string tokenUse, TimeSpan lifetime)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtOptions.TokenUseClaim, tokenUse)
            };

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.Add(lifetime),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(_options.SigningKey), SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
