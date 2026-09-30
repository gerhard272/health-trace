using System.Collections.Generic;

namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Eccezione per errori di validazione dei dati di input.
    /// Errors è un dizionario campo → lista di errori, struttura
    /// compatibile con ValidationProblemDetails (RFC 7807) usato dai controller.
    /// Nota: in coesistenza con FluentValidation.ValidationException usare il namespace pieno.
    /// </summary>
    public class ValidationException : AppException
    {
        public IReadOnlyDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>();

        public ValidationException() : base("Validation failed.") { }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ValidationException(IDictionary<string, string[]> errors)
            : base("Validation failed.")
            => Errors = new Dictionary<string, string[]>(errors);

        public ValidationException(string field, string error)
            : this(new Dictionary<string, string[]> { [field] = [error] }) { }
    }
}