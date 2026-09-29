using Azure.Storage.Queues;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Storage;
using Microsoft.Extensions.Options;

public class QueueExportJobDispatcher : IExportJobDispatcher
{
    private readonly QueueClient _queueClient;

    public QueueExportJobDispatcher(IOptions<BlobStorageOptions> blobOptions)
    {
        // Il QueueTrigger delle Functions (extension Storage.Queues 5.x) si aspetta
        // messaggi codificati in Base64 (default "messageEncoding": "base64").
        // QueueClient invece di default invia testo semplice: senza questa opzione
        // la Function non riesce a decodificare il messaggio e non lo elabora mai.
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
