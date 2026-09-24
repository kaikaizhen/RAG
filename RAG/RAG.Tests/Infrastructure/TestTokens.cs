using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RAG.Tests.Infrastructure
{
    public static class TestTokens
    {
        public const string Issuer = "Kaikai";
        public const string Audience = "Audience";

        public static string Create(
            string secretKey,
            string? subject = "1",
            string issuer = Issuer,
            string audience = Audience,
            string? tokenUse = "access",
            DateTime? notBefore = null,
            DateTime? expires = null)
        {
            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
            if (subject is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, subject));
            if (tokenUse is not null) claims.Add(new Claim("token_use", tokenUse));

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: notBefore ?? now.AddMinutes(-1),
                expires: expires ?? now.AddHours(1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)), SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// alg=none 的未簽章 token
        /// </summary>
        public static string CreateUnsigned(string subject = "1")
        {
            static string B64(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
            var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var header = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = B64($"{{\"nameid\":\"{subject}\",\"token_use\":\"access\",\"iss\":\"{Issuer}\",\"aud\":\"{Audience}\",\"exp\":{exp}}}");
            return $"{header}.{payload}.";
        }
    }
}
