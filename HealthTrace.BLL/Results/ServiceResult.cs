namespace HealthTrace.BLL.Results
{
    /// <summary>
    /// Esiti applicativi del BLL. Scelta dell'enum anziché eccezioni per il flusso di controllo:
    /// I controller mapperanno lo stato in HTTP status; evita try/catch diffusi e rende il contratto esplicito.
    /// Se in futuro serviranno nuovi esiti (es. Unauthorized per il login) si estende l'enum, non si creano tipi ad hoc.
    /// </summary>
    public enum ServiceResultType { Success, ValidationError, BadRequest }

    public class ServiceResult<T>
    {
        public bool Success { get; init; }
        public ServiceResultType Type { get; init; }
        public T? Data { get; init; }
        public IEnumerable<string> Errors { get; init; } = [];

        public static ServiceResult<T> Ok(T data) => new() { Success = true, Type = ServiceResultType.Success, Data = data };
        public static ServiceResult<T> ValidationError(IEnumerable<string> errors) => new() { Type = ServiceResultType.ValidationError, Errors = errors };
        public static ServiceResult<T> BadRequest(string message) => new() { Type = ServiceResultType.BadRequest, Errors = [message] };
    }
}