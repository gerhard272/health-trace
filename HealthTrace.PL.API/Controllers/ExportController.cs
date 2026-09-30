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
        // Richiede un export, risponde 202 con lo stato corrente e il Location del GetById.

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
            // L'utente si ricava per primo: una richiesta non autenticata deve
            // ricevere 401 anche se il body contiene un intervallo non valido.
            var userId = GetUserId();

            if (model.FromDate.HasValue && model.ToDate.HasValue && model.FromDate > model.ToDate)
                throw new BadRequestException("FromDate cannot be later than ToDate.");

            var created = await _exportService.RequestExportAsync(userId, model, cancellationToken);
            await _dispatcher.DispatchAsync(created.Id, cancellationToken);

            // La rilettura serve solo a restituire lo stato aggiornato dal dispatcher
            // (con quello inline l'export può già essere Completed o Failed). La
            // richiesta è stata comunque registrata: se la rilettura non trova nulla
            // si risponde con il modello appena creato invece che con un 404.
            var current = await _exportService.GetByIdAsync(userId, created.Id, cancellationToken)
                ?? created;

            return AcceptedAtAction(nameof(GetById), new { id = created.Id }, current);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/exports
        // Cronologia degli export dell'utente autenticato.

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
        // Stato di un singolo export.

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
        // Scarica il PDF di un export completato; 409 finché non è pronto o se è fallito.

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

        // Estrae l'id dell'utente autenticato; se il claim manca il gestore
        // globale risponde 401 senza ripetere il controllo in ogni action.
        private int GetUserId() => _currentUserService.UserId
            ?? throw new UnauthorizedException();
    }
}
