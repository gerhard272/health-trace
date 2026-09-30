namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Exception for unauthorized access or invalid credentials (401).
    /// The default message can be overridden: the global handler
    /// maps the type to the status code, the body uses the message.
    /// </summary>
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException() : base("Unauthorized.") { }

        public UnauthorizedException(string message) : base(message) { }

        public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
    }
}