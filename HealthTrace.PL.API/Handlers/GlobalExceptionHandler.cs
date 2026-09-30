using HealthTrace.PL.API.Handlers.Interfaces;
using Microsoft.AspNetCore.Diagnostics;

namespace HealthTrace.PL.API.Handlers
{
    /// <summary>
    /// Gestore globale delle eccezioni non intercettate: sceglie status code e body
    /// tramite IErrorDetailsMapper e scrive la risposta in formato ProblemDetails
    /// (RFC 9457). Va registrato con AddExceptionHandler e abilitato con
    /// UseExceptionHandler in un punto della pipeline che copra anche i middleware.
    /// Le richieste abbandonate dal client non sono errori applicativi: in quel caso
    /// l'eccezione viene lasciata propagare.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IErrorDetailsMapper _mapper;
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(
            IErrorDetailsMapper mapper,
            IProblemDetailsService problemDetailsService,
            ILogger<GlobalExceptionHandler> logger)
        {
            _mapper = mapper;
            _problemDetailsService = problemDetailsService;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (httpContext.Response.HasStarted)
                return false;

            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
                return false;

            var problem = _mapper.Map(exception);
            problem.Instance = httpContext.Request.Path.ToString();

            using (_logger.BeginScope(ExceptionLogProperties.For(exception)))
            {
                if (problem.Status >= StatusCodes.Status500InternalServerError)
                {
                    _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                        httpContext.Request.Method, httpContext.Request.Path);
                }
                else
                {
                    _logger.LogWarning("{ErrorType} on {Method} {Path}: {ErrorMessage}",
                        exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
                }
            }

            httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

            var written = await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem
            });

            return written;
        }
    }
}