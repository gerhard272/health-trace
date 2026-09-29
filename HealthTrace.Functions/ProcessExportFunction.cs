using System.Text.Json;
using Azure.Storage.Queues.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HealthTrace.Functions;

public class ProcessExportFunction
{
    private readonly IExportService _exportService;
    private readonly ILogger<ProcessExportFunction> _logger;

    public ProcessExportFunction(IExportService exportService, 
        ILogger<ProcessExportFunction> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    [Function(nameof(ProcessExportFunction))]
    public async Task Run(
        [QueueTrigger("export-requests", Connection = "AzureWebJobsStorage")] 
            QueueMessage message,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(message.MessageText, out var exportRequestId))
        {
            _logger.LogError("Invalid queue message, expected an integer export request id: {MessageText}", message.MessageText);
            return; // non ritentare un messaggio strutturalmente invalido
        }

        _logger.LogInformation("Processing export request {ExportRequestId}", 
            exportRequestId);

        await _exportService.ProcessExportAsync(exportRequestId, cancellationToken);

        _logger.LogInformation("Export request {ExportRequestId} processed", 
            exportRequestId);
    }
}