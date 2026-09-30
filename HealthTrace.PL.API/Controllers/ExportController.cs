using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
using HealthTrace.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/exports")]
    public class ExportsController : ControllerBase
    {
        private const string JsonContentType = "application/json";
        private const string PdfContentType = "application/pdf";

        private readonly IExportService _exportService;
        private readonly IExportJobDispatcher _dispatcher;
        private readonly ICurrentUserService _currentUserService;

        public ExportsController(
            IExportService exportService,
            IExportJobDispatcher dispatcher,
            ICurrentUserService currentUserService)
        {
            _exportService = exportService;
            _dispatcher = dispatcher;
            _currentUserService = currentUserService;
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // POST: api/exports/request
        // Requests an export; returns 202 with the current status and the GetById Location.

        [HttpPost("request")]
        [Produces(JsonContentType)]
        [ProducesResponseType(typeof(ExportRequestModel), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExportRequestModel>> RequestExport(
            [FromBody] ExportRequestCreateModel model,
            CancellationToken cancellationToken)
        {
            // Resolve the user first: an unauthenticated request must get 401
            // even if the body contains an invalid date range.
            var userId = GetUserId();

            if (model.FromDate.HasValue && model.ToDate.HasValue && model.FromDate > model.ToDate)
                throw new BadRequestException("FromDate cannot be later than ToDate.");

            var created = await _exportService.RequestExportAsync(userId, model, cancellationToken);
            await _dispatcher.DispatchAsync(created.Id, cancellationToken);

            // Re-read only to return the status updated by the dispatcher (with the
            // inline one the export may already be Completed or Failed). The request
            // has been stored anyway: if the re-read finds nothing, return the model
            // just created instead of a 404.
            var current = await _exportService.GetByIdAsync(userId, created.Id, cancellationToken)
                ?? created;

            return AcceptedAtAction(nameof(GetById), new { id = created.Id }, current);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/exports
        // Export history of the authenticated user.

        [HttpGet]
        [Produces(JsonContentType)]
        [ProducesResponseType(typeof(IReadOnlyList<ExportRequestModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<ExportRequestModel>>> GetHistory(
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var history = await _exportService.GetHistoryAsync(userId, cancellationToken);
            return Ok(history);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/exports/5
        // Status of a single export.

        [HttpGet("{id:int}")]
        [Produces(JsonContentType)]
        [ProducesResponseType(typeof(ExportRequestModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExportRequestModel>> GetById(
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var export = await _exportService.GetByIdAsync(userId, id, cancellationToken);
            if (export is null)
                throw new NotFoundException("Export", id);

            return Ok(export);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/exports/5/download
        // Downloads the PDF of a completed export; 409 while not ready or if it failed.

        [HttpGet("{id:int}/download")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, PdfContentType)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Download(
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var export = await _exportService.GetByIdAsync(userId, id, cancellationToken);
            if (export is null)
                throw new NotFoundException("Export", id);

            if (export.Status != ExportStatus.Completed)
                throw new ConflictException(
                    export.ErrorMessage
                    ?? $"Export {id} is not ready for download (status: {export.Status}).");

            var file = await _exportService.GetFileAsync(userId, id, cancellationToken);
            if (file is null)
                throw new NotFoundException("Export", id);

            return File(file.Content, file.ContentType, file.FileName);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // Gets the authenticated user id; if the claim is missing the global
        // handler returns 401 without repeating the check in every action.
        private int GetUserId() => _currentUserService.UserId
            ?? throw new UnauthorizedException();
    }
}
