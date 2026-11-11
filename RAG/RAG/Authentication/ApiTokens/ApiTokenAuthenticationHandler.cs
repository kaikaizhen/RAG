using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RAG.Authentication.ApiTokens
{
    /// <summary>
    /// 以 <c>Authorization: Bearer rag_pat_…</c> 驗證應用程式權杖。只對明確掛上 <see cref="ApiPolicies"/> 的 endpoint 生效，
    /// 其他 endpoint 只走 JWT scheme，權杖在那裡等同未登入。
    /// </summary>
    public sealed class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string Scheme = "ApiToken";
        private const string BearerPrefix = "Bearer ";

        private readonly IApiTokenService _tokens;

        public ApiTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiTokenService tokens) : base(options, logger, encoder)
        {
            _tokens = tokens;
        }

        /// <summary>Authorization 標頭帶的是應用程式權杖（JWT scheme 看到時要略過，不當成無效的 JWT）</summary>
        public static bool IsApiToken(HttpRequest request) =>
            request.Headers.Authorization.ToString().StartsWith(BearerPrefix + ApiTokenService.Prefix, StringComparison.Ordinal);

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!IsApiToken(Request)) return AuthenticateResult.NoResult();

            var plaintext = Request.Headers.Authorization.ToString()[BearerPrefix.Length..].Trim();
            var identity = await _tokens.AuthenticateAsync(plaintext, Context.RequestAborted);
            if (identity is null) return AuthenticateResult.Fail("Invalid API token");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, identity.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(ApiTokenClaims.TokenUse, ApiTokenClaims.PersonalAccessToken),
                new(ApiTokenClaims.TokenId, identity.TokenId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(ApiTokenClaims.TokenName, identity.Name)
            };
            claims.AddRange(identity.Scopes.Select(s => new Claim(ApiTokenClaims.Scope, s)));
            claims.AddRange(identity.SpaceIds.Select(id => new Claim(ApiTokenClaims.AllowedSpace, id.ToString(System.Globalization.CultureInfo.InvariantCulture))));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
        }

        // 多個 scheme 的 policy 會對每個 scheme 各 challenge / forbid 一次；JWT scheme 已經寫出回應時這裡不能再動 response
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                Response.Headers.WWWAuthenticate = "Bearer";
            }
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            if (!Response.HasStarted) Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
