using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    public interface IExportService
    {
        // stores the request without generating the PDF
        Task<ExportRequestModel> RequestExportAsync(int userId, 
            ExportRequestCreateModel model, 
            CancellationToken cancellationToken = default);

        // generates the PDF; this is the entry point used by the Azure Function
        Task ProcessExportAsync(int exportRequestId, 
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExportRequestModel>> GetHistoryAsync(int userId, 
            CancellationToken cancellationToken = default);
        Task<ExportRequestModel?> GetByIdAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default);

        // null because it may not exist, may belong to another user or may not be completed
        Task<ExportFileModel?> GetFileAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default);
    }
}