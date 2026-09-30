using HealthTrace.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace HealthTrace.Test.PL.API.Services
{
    /// <summary>
    /// Covers CurrentUserService, which exposes the authenticated user's id read from a single claim.
    /// The claim is the one created by BasicAuthenticationHandler, so in production the value is
    /// always a valid integer: the failed-parsing cases are characterization tests that
    /// document that, if the claim arrived corrupted, the exception is not an AppException and
    /// would therefore return 500 instead of the 401 controllers get from the null value.
    /// </summary>
    public class CurrentUserServiceTests
    {
        private const string UserIdClaim = ClaimTypes.NameIdentifier;
        private const string SubjectClaim = "sub";

        [Theory]
        [InlineData("42", 42)]
        [InlineData("0", 0)]
        [InlineData("-1", -1)]
        [InlineData("+7", 7)]
        [InlineData(" 42 ", 42)]
        [InlineData("2147483647", 2147483647)]
        public void UserId_WithParsableNameIdentifierClaim_ReturnsTheParsedId(string claimValue, int expected)
        {
            // Parsing uses int.Parse with the default styles: surrounding whitespace and a leading sign
            // are accepted, the rest of the format is that of an integer.
            var service = ServiceFor(ContextWith(new Claim(UserIdClaim, claimValue)));

            Assert.Equal(expected, service.UserId);
        }

        [Fact]
        public void UserId_WithoutHttpContext_ReturnsNull()
        {
            // No HttpContext: the access chain stops immediately. This is the case of workers and
            // jobs, where there is no user; controllers turn it into 401.
            var service = ServiceFor(httpContext: null);

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithNullUser_ReturnsNull()
        {
            // User is declared non-nullable on DefaultHttpContext, so this branch is only reachable
            // if someone explicitly sets a null principal: the test does so to cover it.
            var context = new DefaultHttpContext { User = null! };

            var service = ServiceFor(context);

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithoutNameIdentifierClaim_ReturnsNull()
        {
            // Claim present but of another type: the user is authenticated but not identifiable, and the
            // request is rejected as unauthorized instead of failing internally.
            var service = ServiceFor(ContextWith(new Claim(ClaimTypes.Name, "marco.rossi")));

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithOnlySubjectClaim_ReturnsNull()
        {
            // "sub" is the short claim name in JWT tokens, but FindFirst compares the full
            // type: the service only reads the extended URI. The test pins this dependency.
            var service = ServiceFor(ContextWith(new Claim(SubjectClaim, "42")));

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithUnauthenticatedIdentity_StillReturnsTheClaimValue()
        {
            // The service trusts the claim and does not check IsAuthenticated: if an identity without
            // an authentication type carried the same claim, the id would still be accepted.
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(UserIdClaim, "42")]))
            };

            var service = ServiceFor(context);

            Assert.Equal(42, service.UserId);
        }

        [Fact]
        public void UserId_WithMultipleNameIdentifierClaims_ReturnsTheFirst()
        {
            // With two claims of the same type FindFirst returns the first one found: the
            // behavior is that of a list, so order matters.
            var service = ServiceFor(ContextWith(
                new Claim(UserIdClaim, "7"),
                new Claim(UserIdClaim, "9")));

            Assert.Equal(7, service.UserId);
        }

        [Theory]
        [InlineData("abc", typeof(FormatException))]
        [InlineData("", typeof(FormatException))]
        [InlineData("4 2", typeof(FormatException))]
        [InlineData("99999999999", typeof(OverflowException))]
        public void UserId_WithUnparsableNameIdentifierClaim_Throws(string claimValue, Type expected)
        {
            // Current, not desired, behavior: the value always comes from user.Id.ToString(),
            // so these cases do not happen today. If they did, GlobalExceptionHandler
            // would return 500, not 401, because the exception is not an AppException. The test
            // makes any future switch to TryParse visible.
            // A null value is not among the cases: the Claim constructor rejects it, so it
            // cannot reach the service.
            var service = ServiceFor(ContextWith(new Claim(UserIdClaim, claimValue)));

            Assert.Throws(expected, () => service.UserId);
        }

        [Fact]
        public void UserId_IsReadOnEveryAccess()
        {
            // No caching: the property re-reads the context on every access, so a change of
            // principal in the same context is reflected immediately. This is what lets the
            // DbContext record the correct user at save time.
            var context = ContextWith(new Claim(UserIdClaim, "1"));
            var service = ServiceFor(context);

            var first = service.UserId;

            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(UserIdClaim, "2")]));
            var second = service.UserId;

            Assert.Equal(1, first);
            Assert.Equal(2, second);
        }

        private static CurrentUserService ServiceFor(HttpContext? httpContext)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

            return new CurrentUserService(accessor.Object);
        }

        private static DefaultHttpContext ContextWith(params Claim[] claims) =>
            new() { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) };
    }
}
