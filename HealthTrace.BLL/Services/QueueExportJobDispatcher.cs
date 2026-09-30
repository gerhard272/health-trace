using Azure.Storage.Queues;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Storage;
using Microsoft.Extensions.Options;

public class QueueExportJobDispatcher : IExportJobDispatcher
{
    private readonly QueueClient _queueClient;

    public QueueExportJobDispatcher(IOptions<BlobStorageOptions> blobOptions)
    {
        // The Functions QueueTrigger (Storage.Queues extension 5.x) expects
        // Base64-encoded messages (default "messageEncoding": "base64").
        // QueueClient sends plain text by default: without this option the
        // Function cannot decode the message and never processes it.
        _queueClient = new QueueClient(
            blobOptions.Value.ConnectionString,
            "export-requests",
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
    }

    public async Task DispatchAsync(int exportRequestId, CancellationToken cancellationToken = default)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await _queueClient.SendMessageAsync(exportRequestId.ToString(), cancellationToken);
    }
}
