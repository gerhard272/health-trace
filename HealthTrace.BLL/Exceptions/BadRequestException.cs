namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Eccezione per errori applicativi generici che si risolvono in 400 Bad Request.
    /// È il default del ramo AppException del gestore globale: da usare quando
    /// nessuno dei tipi più specifici (Validation, Unauthorized, NotFound) è adatto.
    /// </summary>
    public class BadRequestException : AppException
    {
        public BadRequestException() : base("Bad request.") { }

        public BadRequestException(string message) : base(message) { }

        public BadRequestException(string message, Exception innerException) : base(message, innerException) { }
    }
}