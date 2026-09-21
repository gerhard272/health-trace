using AutoMapper;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Repositories.Interfaces;

namespace HealthTrace.BLL.Services
{
    public class GenericService<TEntity, TModel> : IGenericService<TModel>
        where TEntity : class, new()
        where TModel : class, IModelWithId, new()
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<TEntity> _repository;
        private readonly IMapper _mapper;

        public GenericService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _repository = unitOfWork.Repository<TEntity>();
            _mapper = mapper;
        }

        public async Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(id, cancellationToken);
            return entity == null ? null : _mapper.Map<TModel>(entity);
        }

        public async Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _repository.GetAllAsync(cancellationToken);
            return _mapper.Map<IReadOnlyList<TModel>>(entities);
        }

        public async Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.Map<TEntity>(model);

            await _repository.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<TModel>(entity);
        }

        public async Task<TModel?> UpdateAsync(TModel model, CancellationToken cancellationToken = default)
        {
            var existingEntity = await _repository.GetByIdAsync(model.Id, cancellationToken);

            if (existingEntity == null)
                return null;

            _mapper.Map(model, existingEntity);

            _repository.Update(existingEntity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<TModel>(existingEntity);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(id, cancellationToken);

            if (entity == null)
                return false;

            _repository.Delete(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}