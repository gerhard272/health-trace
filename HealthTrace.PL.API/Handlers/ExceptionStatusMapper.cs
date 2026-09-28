using HealthTrace.BLL.Exceptions;
using HealthTrace.PL.API.Handlers.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Handlers
{
    /// <summary>
    /// Traduce i tipi di eccezione del BLL nel ProblemDetails (RFC 9457) restituito al
    /// client: ValidationException → 400, UnauthorizedException → 401,
    /// NotFoundException → 404, ogni altra AppException → 400, tutto il resto → 500.
    /// Il messaggio delle AppException fa parte del contratto di API e viene esposto nel
    /// body; per le eccezioni non previste il body resta generico e i dettagli restano
    /// solo nel log, con il traceId come chiave di correlazione.
    /// </summary>
    public sealed class ExceptionStatusMapper : IProblemDetailsMapper
    {
        public ProblemDetails Map(Exception exception) => exception switch
        {
            ValidationException validation => ToValidationProblem(validation),
            UnauthorizedException unauthorized => ToUnauthorizedProblem(unauthorized),
            NotFoundException notFound => ToNotFoundProblem(notFound),
            AppException app => ToBadRequestProblem(app),
            _ => ToInternalServerErrorProblem()
        };

        private static ProblemDetails ToValidationProblem(ValidationException exception)
        {
            return new ValidationProblemDetails(
                exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = exception.Message
            };
        }

        private static ProblemDetails ToUnauthorizedProblem(UnauthorizedException exception)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = exception.Message
            };
        }

        private static ProblemDetails ToNotFoundProblem(NotFoundException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = exception.Message
            };

            problem.Extensions["resourceName"] = exception.ResourceName;
            if (exception.Key is not null)
                problem.Extensions["resourceKey"] = exception.Key.ToString();

            return problem;
        }

        private static ProblemDetails ToBadRequestProblem(AppException exception)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = exception.Message
            };
        }

        private static ProblemDetails ToInternalServerErrorProblem()
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error"
            };
        }
    }
}