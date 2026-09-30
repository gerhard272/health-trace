using HealthTrace.BLL.Services.Interfaces;

namespace HealthTrace.BLL.Services
{
    // Synchronous alternative to QueueExportJobDispatcher: processes the export within
    // the same HTTP request. Useful for local runs without the Azure Function.
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