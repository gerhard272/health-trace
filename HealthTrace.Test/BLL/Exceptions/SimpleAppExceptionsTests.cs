using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Copre insieme UnauthorizedException, ConflictException e BadRequestException: condividono lo
    /// stesso contratto (messaggio di default, messaggio personalizzabile, eccezione interna) e non
    /// hanno payload oltre al testo, quindi una Theory evita tre classi con tre test identici.
    /// Nessun mock: sono tipi senza dipendenze.
    /// I messaggi di default contano piu' di quanto sembrino: arrivano al client nel Detail del corpo
    /// di risposta, quindi cambiarli cambia il contratto dell'API, non un dettaglio interno.
    /// </summary>
    public class SimpleAppExceptionsTests
    {
        [Theory]
        [InlineData(typeof(UnauthorizedException), "Unauthorized.")]
        [InlineData(typeof(ConflictException), "Conflict.")]
        [InlineData(typeof(BadRequestException), "Bad request.")]
        public void DefaultMessage_IsTheTextTheClientReceives(Type exceptionType, string expected)
        {
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            Assert.Equal(expected, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Theory]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void CustomMessage_OverridesTheDefault(Type exceptionType)
        {
            const string custom = "Custom message raised by the test";

            var exception = (Exception)Activator.CreateInstance(exceptionType, custom)!;

            Assert.Equal(custom, exception.Message);
        }

        [Theory]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void MessageAndInner_AreBothPreserved(Type exceptionType)
        {
            const string custom = "Custom message raised by the test";
            var inner = new InvalidOperationException("Eccezione interna di test");

            var exception = (Exception)Activator.CreateInstance(exceptionType, custom, inner)!;

            Assert.Equal(custom, exception.Message);
            Assert.Same(inner, exception.InnerException);
        }
    }
}
