using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Handlers.Interfaces
{
    /// <summary>
    /// Turns the exception caught by the global handler into the ProblemDetails
    /// (RFC 9457) returned to the client: it is the only place in the application where an
    /// exception type is associated with an HTTP status code, so BLL services
    /// stay unaware of HTTP and controllers do not have to map exceptions.
    /// Exceptions deriving from AppException declare in their message a
    /// contract meant for the response body; any other exception is an internal
    /// error and must not expose details.
    /// </summary>
    public interface IErrorDetailsMapper
    {
        /// <summary>
        /// Returns the problem data matching the given exception.
        /// </summary>
        ProblemDetails Map(Exception exception);
    }
}