using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Copre NotFoundException, l'unica eccezione che conserva anche i dati usati per costruire il
    /// messaggio: ResourceName e Key alimentano le estensioni del 400/404 restituito dall'handler.
    /// Nessun mock: tipo senza dipendenze.
    /// Il messaggio viene composto qui e non nell'handler, quindi e' formato qui che si decide se
    /// una ricerca senza chiave produce "risorsa non trovata" o un messaggio con una chiave vuota:
    /// i due casi devono restare distinguibili.
    /// </summary>
    public class NotFoundExceptionTests
    {
        private const string ResourceName = "Symptom";

        [Fact]
        public void WithKey_BuildsMessageWithKeyAndKeepsItOnTheException()
        {
            var exception = new NotFoundException(ResourceName, 99);

            Assert.Equal("Symptom with key '99' was not found.", exception.Message);
            Assert.Equal(ResourceName, exception.ResourceName);
            Assert.Equal(99, exception.Key);
        }

        [Fact]
        public void WithoutKey_BuildsMessageWithoutKey()
        {
            // Ricerca per un criterio non identificativo: il messaggio non deve contenere una chiave,
            // e Key resta null cosi' l'handler omette l'estensione resourceKey.
            var exception = new NotFoundException(ResourceName);

            Assert.Equal("Symptom not found.", exception.Message);
            Assert.Equal(ResourceName, exception.ResourceName);
            Assert.Null(exception.Key);
        }

        [Fact]
        public void WithEmptyKey_BuildsMessageWithEmptyKey()
        {
            // Chiave presente ma vuota: e' un caso diverso dalla ricerca senza chiave, quindi il
            // messaggio lo distingue invece di ripetere "not found" come se la chiave mancasse.
            var exception = new NotFoundException(ResourceName, string.Empty);

            Assert.Equal("Symptom with key '' was not found.", exception.Message);
            Assert.Equal(string.Empty, exception.Key);
        }

        [Fact]
        public void Parameterless_UsesGenericResourceNameAndNoKey()
        {
            var exception = new NotFoundException();

            Assert.Equal("Resource not found.", exception.Message);
            Assert.Equal("Resource", exception.ResourceName);
            Assert.Null(exception.Key);
        }

        [Theory]
        [InlineData(typeof(int), 99)]
        [InlineData(typeof(string), "abc")]
        public void Key_IsKeptWithItsOriginalRuntimeType(Type expectedType, object key)
        {
            // L'eccezione conserva l'oggetto com'e': la conversione a testo spetta all'handler, che la
            // fa sull'estensione del corpo. Qui si verifica che il tipo non si perda, cosi' l'handler
            // resta l'unico punto in cui si decide la resa.
            var exception = new NotFoundException(ResourceName, key);

            Assert.IsType(expectedType, exception.Key);
            Assert.Equal(key, exception.Key);
        }

        [Fact]
        public void WithInnerException_KeepsTheKeyAndTheInnerException()
        {
            var inner = new InvalidOperationException("Inner failure raised by the test");

            var exception = new NotFoundException(ResourceName, 99, inner);

            Assert.Equal("Symptom with key '99' was not found.", exception.Message);
            Assert.Equal(99, exception.Key);
            Assert.Same(inner, exception.InnerException);
        }
    }
}
