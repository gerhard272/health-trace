using HealthTrace.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace HealthTrace.Test.PL.API.Services
{
    /// <summary>
    /// Copre CurrentUserService, che espone l'id dell'utente autenticato letto da un solo claim.
    /// Il claim e' quello creato da BasicAuthenticationHandler, quindi in produzione il valore e'
    /// sempre un intero valido: i casi di parsing fallito sono characterization test e servono a
    /// documentare che, se il claim arrivasse corrotto, l'eccezione non e' un'AppException e
    /// quindi risponderebbe 500 invece del 401 che i controller ottengono dal valore nullo.
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
            // Il parsing usa int.Parse con gli stili di default: spazi attorno e segno iniziale
            // sono accettati, il resto del formato e' quello di un intero.
            var service = ServiceFor(ContextWith(new Claim(UserIdClaim, claimValue)));

            Assert.Equal(expected, service.UserId);
        }

        [Fact]
        public void UserId_WithoutHttpContext_ReturnsNull()
        {
            // Nessun HttpContext: la catena di accesso si ferma subito. E' il caso dei worker e dei
            // job, dove l'utente non esiste; i controller lo trasformano in 401.
            var service = ServiceFor(httpContext: null);

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithNullUser_ReturnsNull()
        {
            // User e' dichiarata non-nullable su DefaultHttpContext, quindi questo ramo e' vivo solo
            // se qualcuno imposta esplicitamente un principal nullo: il test lo fa per coprirlo.
            var context = new DefaultHttpContext { User = null! };

            var service = ServiceFor(context);

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithoutNameIdentifierClaim_ReturnsNull()
        {
            // Claim presente ma di altro tipo: l'utente e' autenticato ma non identificabile, e la
            // richiesta viene respinta come non autorizzata invece che fallire internamente.
            var service = ServiceFor(ContextWith(new Claim(ClaimTypes.Name, "marco.rossi")));

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithOnlySubjectClaim_ReturnsNull()
        {
            // "sub" e' il nome abbreviato del claim nei token JWT, ma FindFirst confronta il tipo
            // per intero: il servizio legge solo l'URI esteso. Il test blocca questa dipendenza.
            var service = ServiceFor(ContextWith(new Claim(SubjectClaim, "42")));

            Assert.Null(service.UserId);
        }

        [Fact]
        public void UserId_WithUnauthenticatedIdentity_StillReturnsTheClaimValue()
        {
            // Il servizio si fida del claim e non controlla IsAuthenticated: se un'identity senza
            // tipo di autenticazione portasse lo stesso claim, l'id verrebbe comunque accettato.
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
            // Con due claim dello stesso tipo FindFirst restituisce il primo incontrato: il
            // comportamento che si ottiene e' quello di una lista, quindi l'ordine conta.
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
            // Comportamento attuale, non desiderato: il valore arriva sempre da user.Id.ToString(),
            // quindi questi casi non si verificano oggi. Se si verificassero, GlobalExceptionHandler
            // risponderebbe 500, non 401, perche' l'eccezione non e' un'AppException. Il test serve
            // a rendere visibile ogni eventuale passaggio a TryParse.
            // Un valore null non e' fra i casi: lo rifiuta il costruttore di Claim, quindi non
            // puo' raggiungere il servizio.
            var service = ServiceFor(ContextWith(new Claim(UserIdClaim, claimValue)));

            Assert.Throws(expected, () => service.UserId);
        }

        [Fact]
        public void UserId_IsReadOnEveryAccess()
        {
            // Nessun caching: la property rilegge il contesto a ogni accesso, quindi un cambio di
            // principal nello stesso contesto si riflette subito. E' cio' che permette al
            // DbContext di registrare l'utente corretto al momento del salvataggio.
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
