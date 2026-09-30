namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Exception for resources that were not found (404).
    /// ResourceName identifies the resource type, Key the requested
    /// identifier: both feed the structured 404 body in the handler.
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