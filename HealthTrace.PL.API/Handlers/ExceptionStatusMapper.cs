using HealthTrace.BLL.Exceptions;
using HealthTrace.PL.API.Handlers.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Handlers
{
    /// <summary>
    /// Translates BLL exception types into the ProblemDetails (RFC 9457) returned to the
    /// client: ValidationException → 400, UnauthorizedException → 401,
    /// NotFoundException → 404, ConflictException → 409, any other AppException → 400,
    /// everything else → 500.
    /// The AppException message is part of the API contract and is exposed in the
    /// body; for unexpected exceptions the body stays generic and the details stay
    /// in the log only, with the traceId as correlation key.
    /// </summary>
    public sealed class ExceptionStatusMapper : IErrorDetailsMapper
    {
        public ProblemDetails Map(Exception exception) => exception switch
        {
            ValidationException validation => ToValidationProblem(validation),
            UnauthorizedException unauthorized => ToUnauthorizedProblem(unauthorized),
            NotFoundException notFound => ToNotFoundProblem(notFound),
            ConflictException conflict => ToConflictProblem(conflict),
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

        private static ProblemDetails ToConflictProblem(ConflictException exception)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = exception.Message
            };
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