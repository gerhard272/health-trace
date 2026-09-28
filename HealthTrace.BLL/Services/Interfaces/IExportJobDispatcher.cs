namespace HealthTrace.BLL.Services.Interfaces
{
    // Seam per l'elaborazione asincrona: oggi esegue inline,
    // domani accodera' un messaggio che la Azure Function consumera'.
    public interface IExportJobDispatcher
    {
        Task DispatchAsync(int exportRequestId, CancellationToken cancellationToken = default);
    }
}