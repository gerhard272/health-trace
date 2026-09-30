using AutoMapper;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;

namespace HealthTrace.BLL.Services
{
    public class SymptomService : ISymptomService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Symptom> _symptomRepository;


        public SymptomService(IMapper mapper, IUnitOfWork unitOfWork, IGenericRepository<Symptom> symptomRepository)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _symptomRepository = symptomRepository;

        }

        //gets a specific symptom of a user
        public async Task<SymptomModel?> GetByIdAsync(int userId, int symptomId, CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, symptomId, cancellationToken);
            if (entity == null)
            {
                return null;
            }
            else
            {
                return _mapper.Map<SymptomModel>(entity);
            }
        }

        //gets all the symptoms of a user
        public async Task<IReadOnlyList<SymptomModel>> GetAllByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            //lambda filter to get all the symptoms of a specific user
            var entities = await _symptomRepository.FindAsync(s => s.UserId == userId, cancellationToken);
            return _mapper.Map<IReadOnlyList<SymptomModel>>(entities);

        }

        //creates a new symptom for a user
        public async Task<SymptomModel> CreateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.Map<Symptom>(model);
            entity.UserId = userId;
            await _symptomRepository.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SymptomModel>(entity);
        }

        //updates an existing symptom of a user
        public async Task<SymptomModel?> UpdateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, model.Id, cancellationToken);
            if (entity == null)
            {
                return null;
            }
            _mapper.Map(model, entity);
            //as in CreateAsync, the owner stays the authenticated user: the UserId in the body
            //is ignored (if missing it would be 0, if different it would move the symptom to another user)
            entity.UserId = userId;
            _symptomRepository.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SymptomModel>(entity);
        }

        //deletes an existing symptom of a user
        public async Task<bool> DeleteAsync(int userId, int symptomId, CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, symptomId, cancellationToken);
            if (entity == null)
            {
                return false;
            }
            _symptomRepository.Delete(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        //gets the symptoms of a user on a given date
        public async Task<IReadOnlyList<SymptomModel>> GetByDateAsync(int userId, DateTime date, CancellationToken cancellationToken = default)
        {
            var entities = await _symptomRepository.FindAsync(s => s.UserId == userId && s.EventDate.Date == date.Date, cancellationToken);
            return _mapper.Map<IReadOnlyList<SymptomModel>>(entities);
        }

        //searches a user's symptoms by event name: part of the name is enough
        //("head" finds "Headache"); case is ignored by the database collation
        public async Task<IReadOnlyList<SymptomModel>> GetByNameAsync(int userId, string eventName, CancellationToken cancellationToken = default)
        {
            var term = eventName.Trim();
            var entities = await _symptomRepository.FindAsync(s => s.UserId == userId && s.EventName.Contains(term), cancellationToken);
            return _mapper.Map<IReadOnlyList<SymptomModel>>(entities);
        }

        
        private async Task<Symptom?> GetOwnedEntityAsync(int userId, int symptomId, CancellationToken cancellationToken)
        {
            var entity = await _symptomRepository.FindAsync(s => s.Id == symptomId && s.UserId == userId, cancellationToken);
            return entity.FirstOrDefault();

        }
    }
}
