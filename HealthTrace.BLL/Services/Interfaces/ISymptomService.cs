using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    public interface ISymptomService
    {
        Task<SymptomModel?> GetByIdAsync(int userId, int symptomId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SymptomModel>> GetAllByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<SymptomModel> CreateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default);
        Task<SymptomModel?> UpdateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int userId, int symptomId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SymptomModel>> GetByDateAsync(int userId, DateTime date, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SymptomModel>> GetByNameAsync(int userId, string eventName, CancellationToken cancellationToken = default);
    }
}