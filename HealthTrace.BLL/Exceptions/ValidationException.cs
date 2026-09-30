using System.Collections.Generic;

namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Exception for input data validation errors.
    /// Errors is a field → error list dictionary, a structure
    /// compatible with ValidationProblemDetails (RFC 7807) used by the controllers.
    /// Note: when used alongside FluentValidation.ValidationException, use the fully qualified name.
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