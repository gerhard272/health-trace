namespace HealthTrace.BLL.Services.Interfaces
{
    // Seam for asynchronous processing: either runs the export inline or enqueues
    // a message that the Azure Function consumes.
    public interface IExportJobDispatcher
    {
        Task DispatchAsync(int exportRequestId, CancellationToken cancellationToken = default);
    }
}