using System.Linq.Expressions;

namespace HealthTrace.BLL.Services.Interfaces
{
    public interface IGenericService<TModel> where TModel : class, IModelWithId, new()
    {
        Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default);
        Task<TModel?> UpdateAsync(TModel model, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}