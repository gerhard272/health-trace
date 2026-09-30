using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    public interface IExportService
    {
        // fa la richiesta senza generare il pdf
        Task<ExportRequestModel> RequestExportAsync(int userId, 
            ExportRequestCreateModel model, 
            CancellationToken cancellationToken = default);

        // genera il pdf, qui ho pensato l'integrazione con azure func
        Task ProcessExportAsync(int exportRequestId, 
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExportRequestModel>> GetHistoryAsync(int userId, 
            CancellationToken cancellationToken = default);
        Task<ExportRequestModel?> GetByIdAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default);

        // null perché può non esistere, non appartenere all'utente o non è completed
        Task<ExportFileModel?> GetFileAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default);
    }
}