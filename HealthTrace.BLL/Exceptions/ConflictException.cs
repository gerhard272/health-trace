namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Application exception for a conflict on the resource state.
    /// Translated by the global handler into 409 Conflict.
    /// </summary>
    public class ConflictException : AppException
    {
        public ConflictException() : base("Conflict.") { }
        public ConflictException(string message) : base(message) { }
        public ConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}