using System.Text;
using System.Text.Json;

namespace RAG.Authentication
{
    /// <summary>
    /// JWT 驗證 / 簽發設定（與既有 token contract 相容：issuer=Kaikai、audience=Audience、HS256）
    /// </summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// encryption_config.json 內的 secret 欄位名稱
        /// </summary>
        public const string SecretKeyField = "JWT_SECRET_KEY";

        /// <summary>
        /// 區分 access / refresh token 的 claim
        /// </summary>
        public const string TokenUseClaim = "token_use";
        public const string AccessTokenUse = "access";
        public const string RefreshTokenUse = "refresh";

        /// <summary>
        /// HS256 最小 key 長度（bytes）
        /// </summary>
        private const int MinSecretKeyBytes = 32;

        public string Issuer { get; init; } = "Kaikai";

        public string Audience { get; init; } = "Audience";

        public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromHours(3);

        public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromHours(8);

        public required byte[] SigningKey { get; init; }

        /// <summary>
        /// access token（JwtBearer）與 refresh token 共用的驗證規則
        /// </summary>
        public Microsoft.IdentityModel.Tokens.TokenValidationParameters CreateValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(SigningKey),
            ValidAlgorithms = new[] { Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.Zero
        };

        /// <summary>
        /// 啟動時讀取 JWT 設定；secret 僅從 encryption_config.json 讀取，缺少或無效即 fail-fast，
        /// 絕不自動產生或覆寫 secret。
        /// </summary>
        public static JwtOptions Load(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            var secretFilePath = section["SecretFilePath"];
            if (string.IsNullOrWhiteSpace(secretFilePath))
            {
                // 與既有 Library JwtTokenHelper 讀取相同位置，確保既有 secret 相容
                secretFilePath = Path.Combine(AppContext.BaseDirectory, "encryption_config.json");
            }

            var secretKey = ReadSecretKey(secretFilePath);

            return new JwtOptions
            {
                Issuer = section["Issuer"] is { Length: > 0 } issuer ? issuer : "Kaikai",
                Audience = section["Audience"] is { Length: > 0 } audience ? audience : "Audience",
                SigningKey = Encoding.UTF8.GetBytes(secretKey)
            };
        }

        private static string ReadSecretKey(string secretFilePath)
        {
            if (!File.Exists(secretFilePath))
            {
                throw new InvalidOperationException($"JWT secret file not found: {secretFilePath}");
            }

            string? secretKey;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(secretFilePath));
                secretKey = document.RootElement.TryGetProperty(SecretKeyField, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"JWT secret file is not valid JSON: {secretFilePath}", ex);
            }

            if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith('<'))
            {
                throw new InvalidOperationException($"{SecretKeyField} is missing or still a placeholder in {secretFilePath}");
            }

            if (Encoding.UTF8.GetByteCount(secretKey) < MinSecretKeyBytes)
            {
                throw new InvalidOperationException($"{SecretKeyField} must be at least {MinSecretKeyBytes} bytes for HS256");
            }

            return secretKey;
        }
    }
}
