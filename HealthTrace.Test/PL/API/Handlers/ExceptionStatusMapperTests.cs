using HealthTrace.BLL.Exceptions;
// Il namespace di questo file replica quello del codice sotto test, quindi la
// regola di risoluzione dei namespace annidati non trova da sola il mapper: la
// dichiarazione esplicita serve, come in tutti gli altri punti in cui i due
// namespace hanno lo stesso nome finale.
using HealthTrace.PL.API.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.Test.PL.API.Handlers
{
    /// <summary>
    /// Classe di test per ExceptionStatusMapper, l'unico punto dell'applicazione in cui
    /// un tipo di eccezione viene tradotto in uno status code HTTP e in un corpo
    /// ProblemDetails. Non servono mock ne' un host: il mapper e' puro, non ha
    /// dipendenze e si istanzia direttamente, quindi ogni assertion riguarda un ramo
    /// reale dello switch.
    /// I test verificano la traduzione, non il testo delle singole eccezioni: il
    /// Detail e' sempre il messaggio dell'eccezione ricevuta, costruito dall'eccezione
    /// stessa, mentre scelta di status, titolo, tipo di problema ed estensioni sono
    /// responsabilita' del mapper. Per questo i messaggi sono confrontati con
    /// exception.Message e non scritti per esteso.
    /// Il ramo 500 e' l'unico che non riceve alcun dettaglio: asserire esplicitamente
    /// che il Detail resti nullo blocca la divulgazione di informazioni interne, che
    /// nel doc comment della classe e' indicata come comportamento voluto.
    /// </summary>
    public class ExceptionStatusMapperTests
    {
        private const string ResourceName = "Symptom";
        private const int ResourceKey = 99;

        private const string ValidationTitle = "Validation failed";
        private const string UnauthorizedTitle = "Unauthorized";
        private const string NotFoundTitle = "Not Found";
        private const string ConflictTitle = "Conflict";
        private const string BadRequestTitle = "Bad Request";
        private const string InternalServerErrorTitle = "Internal Server Error";

        private const string ResourceNameExtension = "resourceName";
        private const string ResourceKeyExtension = "resourceKey";

        private readonly ExceptionStatusMapper _mapper = new();

        // --- ValidationException ---

        [Fact]
        public void Map_ValidationException_ReturnsValidationProblemDetailsWithFieldErrors()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["username"] = ["Username already in use"],
                ["cf"] = ["Fiscal code already registered", "Format invalid"]
            };

            var problem = _mapper.Map(new AppValidationException(errors));

            // Tipo concreto diverso da ProblemDetails: e' il segnale che il client usa
            // per sapere che gli errori sono raggruppati per nome di campo.
            var validation = Assert.IsType<ValidationProblemDetails>(problem);
            Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
            Assert.Equal(ValidationTitle, validation.Title);
            Assert.Equal("Validation failed.", validation.Detail);

            // Il raggruppamento per campo e' il contenuto di questo ramo: la stessa
            // chiave conserva piu' messaggi, chiavi diverse restano separate.
            Assert.Equal(2, validation.Errors.Count);
            Assert.Equal(new[] { "Username already in use" }, validation.Errors["username"]);
            Assert.Equal(
                new[] { "Fiscal code already registered", "Format invalid" },
                validation.Errors["cf"]);
        }

        [Fact]
        public void Map_ValidationException_WithoutFieldErrors_ReturnsEmptyErrorDictionary()
        {
            // L'eccezione puo' essere lanciata senza alcun errore per campo: il mapper
            // deve comunque restituire un dizionario vuoto, non un riferimento nullo.
            var problem = _mapper.Map(new AppValidationException());

            var validation = Assert.IsType<ValidationProblemDetails>(problem);
            Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
            Assert.Empty(validation.Errors);
        }

        // --- UnauthorizedException ---

        [Fact]
        public void Map_UnauthorizedException_Returns401WithExceptionMessageAsDetail()
        {
            // Messaggio unico per credenziali inesistenti e credenziali errate: e' una
            // scelta di sicurezza per non permettere l'enumerazione degli account.
            var exception = new UnauthorizedException("Invalid username or password");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
            Assert.Equal(UnauthorizedTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
            Assert.Empty(problem.Extensions);
        }

        // --- NotFoundException ---

        [Fact]
        public void Map_NotFoundException_Returns404WithResourceExtensions()
        {
            var exception = new NotFoundException(ResourceName, ResourceKey);

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal(NotFoundTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);

            // Le estensioni sono informazioni che il client non riceveva prima della
            // traduzione a eccezioni: dicono quale risorsa e con quale chiave si cercava.
            Assert.Equal(ResourceName, Assert.IsType<string>(problem.Extensions[ResourceNameExtension]));
            Assert.Equal("99", Assert.IsType<string>(problem.Extensions[ResourceKeyExtension]));
        }

        [Fact]
        public void Map_NotFoundException_WithoutKey_OmitsResourceKeyExtension()
        {
            // Senza chiave non viene emessa un'estensione vuota: il client deve poter
            // distinguere "cercata senza chiave" da "cercata con chiave vuota".
            var problem = _mapper.Map(new NotFoundException(ResourceName));

            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal(ResourceName, Assert.IsType<string>(problem.Extensions[ResourceNameExtension]));
            Assert.False(problem.Extensions.ContainsKey(ResourceKeyExtension));
        }

        [Theory]
        [InlineData(99, "99")]
        [InlineData("abc", "abc")]
        public void Map_NotFoundException_ConvertsKeyToText(object key, string expected)
        {
            // La chiave arriva come object e nel corpo deve viaggiare come testo.
            var problem = _mapper.Map(new NotFoundException(ResourceName, key));

            Assert.Equal(expected, Assert.IsType<string>(problem.Extensions[ResourceKeyExtension]));
        }

        // --- ConflictException ---

        [Fact]
        public void Map_ConflictException_Returns409WithExceptionMessageAsDetail()
        {
            var exception = new ConflictException("The symptom is already under analysis.");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.Equal(ConflictTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
        }

        // --- Altra AppException ---

        [Fact]
        public void Map_OtherAppException_Returns400WithBadRequestTitle()
        {
            // BadRequestException deriva da AppException come i tipi precedenti, ma
            // nessuno dei rami specifici la intercetta: e' la prova che i rami validi
            // sono valutati prima del ramo generico e non vengono assorbiti da esso.
            var exception = new BadRequestException("The uploaded file is too large.");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
            Assert.Equal(BadRequestTitle, problem.Title);
            Assert.NotEqual(ValidationTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
        }

        // --- Eccezione non prevista ---

        [Theory]
        [InlineData(typeof(FormatException))]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentNullException))]
        [InlineData(typeof(Exception))]
        public void Map_UnexpectedException_Returns500WithoutDetail(Type exceptionType)
        {
            // Nessuna di queste eccezioni e' nota al mapper, quindi il ramo finale
            // deve produrre lo stesso 500 per tutte.
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
            Assert.Equal(InternalServerErrorTitle, problem.Title);

            // Nessun dettaglio esposto: il corpo resta generico e i dettagli restano
            // nel log, con il traceId come chiave di correlazione.
            Assert.Null(problem.Detail);
            Assert.Empty(problem.Extensions);
        }
    }
}
