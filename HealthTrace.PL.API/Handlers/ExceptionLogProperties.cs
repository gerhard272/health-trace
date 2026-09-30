using HealthTrace.BLL.Exceptions;

namespace HealthTrace.PL.API.Handlers
{
    /// <summary>
    /// Fornisce un metodo per ottenere le proprietà da loggare per un'eccezione specifica.
    /// </summary>
    public static class ExceptionLogProperties
    {
        public static IReadOnlyDictionary<string, object?> For(Exception exception)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ErrorType"] = exception.GetType().Name
            };

            switch (exception)
            {
                case NotFoundException notFound:
                    properties["ResourceName"] = notFound.ResourceName;
                    if (notFound.Key is not null)
                        properties["ResourceKey"] = notFound.Key;
                    break;

                case ValidationException validation:
                    properties["ErrorFields"] = validation.Errors.Keys.ToArray();
                    properties["ErrorCount"] = validation.Errors.Sum(entry => entry.Value?.Length ?? 0);
                    break;
            }

            return properties;
        }
    }
}
