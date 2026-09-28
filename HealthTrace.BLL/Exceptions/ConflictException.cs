namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Eccezione applicativa per un conflitto sullo stato della risorsa.
    /// Tradotta dal gestore globale in 409 Conflict.
    /// </summary>
    public class ConflictException : AppException
    {
        public ConflictException() : base("Conflict.") { }
        public ConflictException(string message) : base(message) { }
        public ConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}