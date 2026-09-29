using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Copre ValidationException, l'unica eccezione che porta un payload oltre al messaggio: il
    /// dizionario campo -> errori che diventa il corpo del 400. Nessun mock: tipo senza dipendenze.
    /// Il messaggio conta quanto il payload, perche' finisce nel Detail della risposta: i
    /// costruttori che non lo ricevono impongono "Validation failed.".
    /// Nota: FluentValidation non viene importato, quindi ValidationException risolve senza
    /// ambigua' alla classe del BLL, come nella nota dei file in produzione.
    /// </summary>
    public class ValidationExceptionTests
    {
        private const string DefaultMessage = "Validation failed.";
        private const string FieldKey = "cf";
        private const string FieldMessage = "Format invalid";
        private const string GeneralKey = "general";
        private const string GeneralMessage = "Username already in use";

        [Fact]
        public void Parameterless_UsesDefaultMessageAndEmptyErrors()
        {
            // Puo' essere lanciata senza alcun errore da campo: Errors resta vuota, non null, altrimenti
            // il client riceverebbe un corpo senza la struttura attesa.
            var exception = new ValidationException();

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.NotNull(exception.Errors);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void MessageOverload_KeepsCustomMessageAndEmptyErrors()
        {
            var exception = new ValidationException(DefaultMessage);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void MessageAndInnerOverload_KeepsMessageAndInnerAndEmptyErrors()
        {
            var inner = new InvalidOperationException("Inner failure raised by the test");

            var exception = new ValidationException(DefaultMessage, inner);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Same(inner, exception.InnerException);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void ErrorsDictionary_CopiesFieldsAndKeepsDefaultMessage()
        {
            var errors = new Dictionary<string, string[]> { [FieldKey] = [FieldMessage] };

            var exception = new ValidationException(errors);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Equal(new[] { FieldMessage }, exception.Errors[FieldKey]);
        }

        [Fact]
        public void ErrorsDictionary_IsNotAffectedByLaterChangesToTheSource()
        {
            // Il dizionario viene ricreato nel costruttore: il servizio che ha costruito la
            // ValidationFailureResult puo' continuare a usare la sua raccolta senza modificare
            // l'eccezione che sta per attraversare i layer.
            var errors = new Dictionary<string, string[]> { [FieldKey] = [FieldMessage] };

            var exception = new ValidationException(errors);
            errors[FieldKey] = ["changed after construction"];
            errors["other"] = ["new field"];

            Assert.Equal(new[] { FieldMessage }, exception.Errors[FieldKey]);
            Assert.Single(exception.Errors);
        }

        [Fact]
        public void ErrorsDictionary_SharesTheErrorArraysByReference()
        {
            // Limite noto della copia, che e' superficiale: il dizionario e' ricreato ma gli array dei
            // messaggi restano condivisi, quindi una sostituzione nell'array di origine si vede
            // nell'eccezione. Il test documenta il comportamento, non lo approva.
            var messages = new[] { FieldMessage };
            var exception = new ValidationException(new Dictionary<string, string[]> { [FieldKey] = messages });

            messages[0] = "changed";

            Assert.Equal("changed", exception.Errors[FieldKey][0]);
        }

        [Theory]
        [InlineData(FieldKey, FieldMessage)]
        [InlineData(GeneralKey, GeneralMessage)]
        public void FieldAndErrorOverload_KeepsFieldAndMessageVerbatim(string field, string error)
        {
            // Usato anche per errori senza campo proprio, sotto la chiave "general": la chiave deve
            // arrivare al client cosi' com'e', altrimenti il frontend non sa dove mostrare l'errore.
            var exception = new ValidationException(field, error);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Equal(new[] { error }, exception.Errors[field]);
            Assert.Single(exception.Errors);
        }
    }
}
