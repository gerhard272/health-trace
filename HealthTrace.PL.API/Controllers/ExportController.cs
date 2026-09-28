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

        // richiede un export, risponde 202 e id
        [HttpPost("request")]
        public async Task<IActionResult> RequestExport(ExportRequestCreateModel model,
            CancellationToken cancellationToken)
        {
            if (model.FromDate.HasValue && model.ToDate.HasValue && model.FromDate > model.ToDate)
                throw new BadRequestException("FromDate cannot be later than ToDate.");

            var userId = GetUserId();

            var created = await _exportService.RequestExportAsync(userId, model, cancellationToken);
            await _dispatcher.DispatchAsync(created.Id, cancellationToken);

            var current = await _exportService.GetByIdAsync(userId, created.Id, cancellationToken);
            if (current is null)
                throw new NotFoundException("Export", created.Id);

            return AcceptedAtAction(nameof(GetById), new { id = created.Id }, current);
        }

        // cronologia export
        [HttpGet]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var history = await _exportService.GetHistoryAsync(userId, cancellationToken);
            return Ok(history);
        }

        // Stato di un singolo export
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var export = await _exportService
                                    .GetByIdAsync(userId, id, cancellationToken);
            if (export is null)
                throw new NotFoundException("Export", id);

            return Ok(export);
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
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