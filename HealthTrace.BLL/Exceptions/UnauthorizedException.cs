namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Eccezione per accesso non autorizzato o token non valido (401).
    /// Messaggio di default overridabile: il handler del Task 101
    /// mappa il tipo sullo status code, il body usa il messaggio.
    /// </summary>
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException() : base("Unauthorized.") { }

        public UnauthorizedException(string message) : base(message) { }

        public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
    }
}