using Library.Http;
using System.Net;
using System.Security.Claims;

namespace RAG.Authentication
{
    public class CurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int UserId
        {
            get
            {
                var principal = _httpContextAccessor.HttpContext?.User;

                if (!TryGetUserId(principal, out var userId))
                {
                    throw new HttpException(HttpStatusCode.Unauthorized, "Unauthorized");
                }

                return userId;
            }
        }

        /// <summary>
        /// 從已驗證 principal 取得唯一且為正整數的 <see cref="ClaimTypes.NameIdentifier"/>；其他情況一律失敗
        /// </summary>
        public static bool TryGetUserId(ClaimsPrincipal? principal, out int userId)
        {
            userId = 0;

            if (principal?.Identity?.IsAuthenticated != true) return false;

            var claims = principal.FindAll(ClaimTypes.NameIdentifier).ToList();
            if (claims.Count != 1) return false;

            return int.TryParse(claims[0].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out userId)
                && userId > 0;
        }
    }
}
