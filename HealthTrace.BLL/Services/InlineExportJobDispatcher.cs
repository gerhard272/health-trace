using HealthTrace.BLL.Services.Interfaces;

namespace HealthTrace.BLL.Services
{
    // Implementazione temporanea: elabora l'export nella stessa richiesta HTTP.
    // Verra' sostituita da un QueueExportJobDispatcher quando arrivera' la Azure Function.
    public class InlineExportJobDispatcher : IExportJobDispatcher
    {
        private readonly IExportService _exportService;

        public InlineExportJobDispatcher(IExportService exportService)
        {
            _exportService = exportService;
        }

        public Task DispatchAsync(int exportRequestId, 
            CancellationToken cancellationToken = default)
            => _exportService.ProcessExportAsync(exportRequestId, cancellationToken);
    }
}