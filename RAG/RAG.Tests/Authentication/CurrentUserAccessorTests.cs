using Library.Http;
using Microsoft.AspNetCore.Http;
using RAG.Authentication;
using System.Net;
using System.Security.Claims;

namespace RAG.Tests.Authentication
{
    public class CurrentUserAccessorTests
    {
        private static CurrentUserAccessor Create(ClaimsPrincipal? principal)
        {
            var context = new DefaultHttpContext();
            if (principal is not null) context.User = principal;
            return new CurrentUserAccessor(new HttpContextAccessor { HttpContext = context });
        }

        private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

        [Fact]
        public void Reads_NameIdentifier_as_user_id()
        {
            var accessor = Create(Authenticated(new Claim(ClaimTypes.NameIdentifier, "42")));

            Assert.Equal(42, accessor.UserId);
        }

        public static TheoryData<ClaimsPrincipal?> InvalidPrincipals => new()
        {
            null,
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "42") })), // 未驗證
            Authenticated(),
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "abc")),
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "0")),
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "-5")),
            Authenticated(new Claim(ClaimTypes.NameIdentifier, " 42")),
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.NameIdentifier, "2")),
            Authenticated(new Claim("sub", "42")),
        };

        [Theory]
        [MemberData(nameof(InvalidPrincipals))]
        public void Fails_closed_with_401(ClaimsPrincipal? principal)
        {
            var accessor = Create(principal);

            var ex = Assert.Throws<HttpException>(() => accessor.UserId);
            Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        }

        [Fact]
        public void Fails_closed_without_http_context()
        {
            var accessor = new CurrentUserAccessor(new HttpContextAccessor());

            Assert.Throws<HttpException>(() => accessor.UserId);
        }
    }
}
