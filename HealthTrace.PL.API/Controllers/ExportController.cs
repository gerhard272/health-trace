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
                return BadRequest("FromDate non puo' essere successiva a ToDate.");

            var userId = _currentUserService.UserId!.Value;

            var created = await _exportService.RequestExportAsync(userId, model, cancellationToken);
            await _dispatcher.DispatchAsync(created.Id, cancellationToken);

            var current = await _exportService.GetByIdAsync(userId, created.Id, cancellationToken);
            return AcceptedAtAction(nameof(GetById), new { id = created.Id }, current);
        }

        // cronologia export
        [HttpGet]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!.Value;
            var history = await _exportService.GetHistoryAsync(userId, cancellationToken);
            return Ok(history);
        }

        // Stato di un singolo export
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!.Value;
            var export = await _exportService
                                    .GetByIdAsync(userId, id, cancellationToken);
            return export == null ? NotFound() : Ok(export);
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!.Value;

            var export = await _exportService.GetByIdAsync(userId, id, cancellationToken);
            if (export == null)
                return NotFound();

            if (export.Status != ExportStatus.Completed)
                return Conflict(new { status = export.Status.ToString(), export.ErrorMessage });

            var file = await _exportService.GetFileAsync(userId, id, cancellationToken);
            return file == null
                ? NotFound()
                : File(file.Content, file.ContentType, file.FileName);
        }
    }
}