namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Exception for generic application errors that result in 400 Bad Request.
    /// It is the default of the AppException branch in the global handler: use it when
    /// none of the more specific types (Validation, Unauthorized, NotFound) fits.
    /// </summary>
    public class BadRequestException : AppException
    {
        public BadRequestException() : base("Bad request.") { }

        public BadRequestException(string message) : base(message) { }

        public BadRequestException(string message, Exception innerException) : base(message, innerException) { }
    }
}