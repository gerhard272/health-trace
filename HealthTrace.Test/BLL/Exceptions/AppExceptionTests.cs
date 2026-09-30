using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Copre il contratto della classe base AppException, da cui discendono tutte le eccezioni
    /// applicative. Nessun mock: sono tipi senza dipendenze, si costruiscono direttamente.
    /// La gerarchia non e' un dettaglio interno: GlobalExceptionHandler ed ExceptionStatusMapper
    /// decidono status code e corpo della risposta in base a questa discendenza, quindi un'eccezione
    /// che smettesse di derivare da AppException finirebbe nel ramo 500 invece che nei rami 4xx.
    /// </summary>
    public class AppExceptionTests
    {
        [Fact]
        public void AppException_IsAbstract()
        {
            // Nessuno puo' lanciare una AppException generica: il ramo dell'handler che la intercetta
            // deve poter contare su dettagli (messaggio, payload) messi a disposizione dalle derivate.
            Assert.True(typeof(AppException).IsAbstract);
        }

        [Theory]
        [InlineData(typeof(ValidationException))]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(NotFoundException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void DerivedExceptions_AreCatchableAsAppException(Type exceptionType)
        {
            // Tutte e cinque le eccezioni applicative devono restare intercettabili dal ramo
            // generico dell'handler; i test dei servizi coprono gia' i tipi effettivamente lanciati.
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            Assert.IsAssignableFrom<AppException>(exception);
        }
    }
}
