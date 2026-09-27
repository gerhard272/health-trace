namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Eccezione per risorse non trovate (404).
    /// ResourceName identifica il tipo di risorsa, Key l'identificativo
    /// richiesto: entrambi alimentano il body strutturato del 404 nel handler.
    /// </summary>
    public class NotFoundException : AppException
    {
        public string ResourceName { get; }
        public object? Key { get; }

        public NotFoundException() : this("Resource", null) { }

        public NotFoundException(string resourceName, object? key = null)
            : base(key is null
                ? $"{resourceName} not found."
                : $"{resourceName} with key '{key}' was not found.")
        {
            ResourceName = resourceName;
            Key = key;
        }

        public NotFoundException(string resourceName, object? key, Exception innerException)
            : base($"{resourceName} with key '{key}' was not found.", innerException)
        {
            ResourceName = resourceName;
            Key = key;
        }
    }
}