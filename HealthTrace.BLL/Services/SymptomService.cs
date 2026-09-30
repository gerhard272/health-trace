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

        //metodo per ottenere un sintomo specifico di un utente
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

        //metodo per ottenere tutti i sintomi di un utente
        public async Task<IReadOnlyList<SymptomModel>> GetAllByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            //filter con funzione lambda per ottenere tutti i sintomi di un utente specifico
            var entities = await _symptomRepository.FindAsync(s => s.UserId == userId, cancellationToken);
            return _mapper.Map<IReadOnlyList<SymptomModel>>(entities);

        }

        //metodo per creare un nuovo sintomo per un utente
        public async Task<SymptomModel> CreateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.Map<Symptom>(model);
            entity.UserId = userId;
            await _symptomRepository.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SymptomModel>(entity);
        }

        //metodo per aggiornare un sintomo esistente di un utente
        public async Task<SymptomModel?> UpdateAsync(int userId, SymptomModel model, CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, model.Id, cancellationToken);
            if (entity == null)
            {
                return null;
            }
            _mapper.Map(model, entity);
            //come in CreateAsync, il proprietario resta l'utente autenticato: lo UserId del body
            //viene ignorato (se mancante varrebbe 0, se diverso sposterebbe il sintomo a un altro utente)
            entity.UserId = userId;
            _symptomRepository.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SymptomModel>(entity);
        }

        //metodo per eliminare un sintomo esistente di un utente
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

        //metodo per ottenere un sintomo specifico di un utente dalla data
        public async Task<IReadOnlyList<SymptomModel>> GetByDateAsync(int userId, DateTime date, CancellationToken cancellationToken = default)
        {
            var entities = await _symptomRepository.FindAsync(s => s.UserId == userId && s.EventDate.Date == date.Date, cancellationToken);
            return _mapper.Map<IReadOnlyList<SymptomModel>>(entities);
        }

        //metodo per cercare i sintomi di un utente per nome dell'evento: basta una parte del nome
        //("testa" trova "Mal di testa"); maiuscole/minuscole le ignora la collation del database
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
