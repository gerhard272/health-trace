using System.Linq.Expressions;
using AutoMapper;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services;
using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;

namespace HealthTrace.Test.BLL.Services
{
    /// <summary>
    /// Class di test per la classe SymptomService, focalizzata sui metodi
    /// GetByIdAsync, GetAllByUserIdAsync, CreateAsync, UpdateAsync,
    /// DeleteAsync, GetByDateAsync e GetByNameAsync.
    /// </summary>
    public class SymptomServiceTests
    {
        private const int UserId = 1;
        private const int OtherUserId = 2;
        private const int SymptomId = 10;
        private const string EventName = "febbre";

        private static readonly DateTime EventDate = new(2026, 9, 26, 10, 30, 0);
        private static readonly IReadOnlyList<Symptom> NoSymptoms = Array.Empty<Symptom>();

        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGenericRepository<Symptom>> _repository = new();

        private Expression<Func<Symptom, bool>>? _capturedPredicate;

        private static SymptomModel ValidModel() => new()
        {
            Id = SymptomId,
            UserId = UserId,
            EventName = EventName,
            EventDate = EventDate
        };

        private static Symptom ValidEntity(int id = SymptomId, int userId = UserId) => new()
        {
            Id = id,
            UserId = userId,
            EventName = EventName,
            EventDate = EventDate
        };

        private SymptomService CreateService() =>
            new(_mapper.Object, _unitOfWork.Object, _repository.Object);

        private void SetupFindAsync(IReadOnlyList<Symptom> result)
        {
            _capturedPredicate = null;
            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<Symptom, bool>>, CancellationToken>((expr, _) => _capturedPredicate = expr)
                .ReturnsAsync(result);
        }

        private void SetupListMapping(IReadOnlyList<SymptomModel> models)
        {
            _mapper
                .Setup(m => m.Map<IReadOnlyList<SymptomModel>>(It.IsAny<IReadOnlyList<Symptom>>()))
                .Returns(models);
        }

        // --- GetByIdAsync ---

        [Fact]
        public async Task GetByIdAsync_OwnedSymptom_ReturnsMappedModel()
        {
            var entity = ValidEntity();
            var expected = new SymptomModel { Id = SymptomId, UserId = UserId, EventName = EventName };
            SetupFindAsync(new[] { entity });
            _mapper.Setup(m => m.Map<SymptomModel>(It.IsAny<Symptom>())).Returns(expected);
            var service = CreateService();

            var result = await service.GetByIdAsync(UserId, SymptomId);

            Assert.Same(expected, result);
            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_NotFound_ReturnsNull()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            var result = await service.GetByIdAsync(UserId, SymptomId);

            Assert.Null(result);
            _mapper.Verify(m => m.Map<SymptomModel>(It.IsAny<Symptom>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_PredicateMatchesSymptomIdAndOwner()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            await service.GetByIdAsync(UserId, SymptomId);

            Assert.NotNull(_capturedPredicate);
            var predicate = _capturedPredicate!.Compile();

            Assert.True(predicate(new Symptom { Id = SymptomId, UserId = UserId }));
            Assert.False(predicate(new Symptom { Id = SymptomId, UserId = OtherUserId }));
            Assert.False(predicate(new Symptom { Id = SymptomId + 1, UserId = UserId }));
        }

        // --- GetAllByUserIdAsync ---

        [Fact]
        public async Task GetAllByUserIdAsync_ReturnsMappedList()
        {
            var entities = new[] { ValidEntity(1), ValidEntity(2) };
            var expected = new List<SymptomModel> { new() { Id = 1 }, new() { Id = 2 } };
            SetupFindAsync(entities);
            SetupListMapping(expected);
            var service = CreateService();

            var result = await service.GetAllByUserIdAsync(UserId);

            Assert.Equal(expected, result);
            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // --- CreateAsync ---

        [Fact]
        public async Task CreateAsync_ForcesUserIdOnPersistedEntity()
        {
            // Il modello arriva con UserId "sbagliato": il service deve sovrascriverlo.
            var model = ValidModel();
            model.UserId = OtherUserId;

            var mappedEntity = new Symptom { UserId = OtherUserId, EventName = EventName, EventDate = EventDate };
            var mappedBack = new SymptomModel { Id = SymptomId, UserId = OtherUserId, EventName = EventName };
            _mapper.Setup(m => m.Map<Symptom>(It.IsAny<SymptomModel>())).Returns(mappedEntity);
            _mapper.Setup(m => m.Map<SymptomModel>(It.IsAny<Symptom>())).Returns(mappedBack);

            Symptom? persisted = null;
            _repository
                .Setup(r => r.AddAsync(It.IsAny<Symptom>(), It.IsAny<CancellationToken>()))
                .Callback<Symptom, CancellationToken>((s, _) => persisted = s)
                .Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var service = CreateService();

            var result = await service.CreateAsync(UserId, model);

            Assert.NotNull(persisted);
            Assert.Equal(UserId, persisted!.UserId);
            Assert.Same(mappedBack, result);
            _repository.Verify(r => r.AddAsync(It.IsAny<Symptom>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_PropagatesCancellationToken()
        {
            var model = ValidModel();
            CancellationToken? addToken = null, saveToken = null;

            _mapper.Setup(m => m.Map<Symptom>(It.IsAny<SymptomModel>()))
                .Returns(new Symptom { EventName = EventName, EventDate = EventDate });
            _mapper.Setup(m => m.Map<SymptomModel>(It.IsAny<Symptom>()))
                .Returns(new SymptomModel { Id = SymptomId });
            _repository
                .Setup(r => r.AddAsync(It.IsAny<Symptom>(), It.IsAny<CancellationToken>()))
                .Callback<Symptom, CancellationToken>((_, ct) => addToken = ct)
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var result = await service.CreateAsync(UserId, model, cts.Token);

            Assert.NotNull(result);
            Assert.Equal(cts.Token, addToken);
            Assert.Equal(cts.Token, saveToken);
        }

        // --- UpdateAsync ---

        [Fact]
        public async Task UpdateAsync_NotOwned_ReturnsNullWithoutWriting()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            var result = await service.UpdateAsync(UserId, ValidModel());

            Assert.Null(result);
            _mapper.Verify(m => m.Map(It.IsAny<SymptomModel>(), It.IsAny<Symptom>()), Times.Never);
            _repository.Verify(r => r.Update(It.IsAny<Symptom>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_Owned_MapsInPlaceUpdatesAndSaves()
        {
            var entity = ValidEntity();
            var model = ValidModel();
            var updated = new SymptomModel { Id = SymptomId, UserId = UserId, EventName = EventName };
            SetupFindAsync(new[] { entity });
            _mapper.Setup(m => m.Map<SymptomModel>(It.IsAny<Symptom>())).Returns(updated);
            var service = CreateService();

            var result = await service.UpdateAsync(UserId, model);

            Assert.Same(updated, result);
            _mapper.Verify(m => m.Map(model, entity), Times.Once);
            _repository.Verify(r => r.Update(entity), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_PropagatesCancellationToken()
        {
            var entity = ValidEntity();
            var model = ValidModel();
            CancellationToken? findToken = null, saveToken = null;

            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<Symptom, bool>>, CancellationToken>((_, ct) => findToken = ct)
                .ReturnsAsync(new[] { entity });
            _mapper.Setup(m => m.Map<SymptomModel>(It.IsAny<Symptom>())).Returns(new SymptomModel { Id = SymptomId });
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var result = await service.UpdateAsync(UserId, model, cts.Token);

            Assert.NotNull(result);
            Assert.Equal(cts.Token, findToken);
            Assert.Equal(cts.Token, saveToken);
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_NotOwned_ReturnsFalseWithoutWriting()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            var result = await service.DeleteAsync(UserId, SymptomId);

            Assert.False(result);
            _repository.Verify(r => r.Delete(It.IsAny<Symptom>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_Owned_DeletesAndSaves_ReturnsTrue()
        {
            var entity = ValidEntity();
            SetupFindAsync(new[] { entity });
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var service = CreateService();

            var result = await service.DeleteAsync(UserId, SymptomId);

            Assert.True(result);
            _repository.Verify(r => r.Delete(entity), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_PropagatesCancellationToken()
        {
            var entity = ValidEntity();
            CancellationToken? findToken = null, deleteToken = null, saveToken = null;

            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<Symptom, bool>>, CancellationToken>((_, ct) => findToken = ct)
                .ReturnsAsync(new[] { entity });
            _repository
                .Setup(r => r.Delete(It.IsAny<Symptom>()))
                .Callback<Symptom>(_ => deleteToken = CancellationToken.None);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var result = await service.DeleteAsync(UserId, SymptomId, cts.Token);

            Assert.True(result);
            Assert.Equal(cts.Token, findToken);
            Assert.Equal(cts.Token, saveToken);
        }

        // --- GetByDateAsync ---

        [Fact]
        public async Task GetByDateAsync_ReturnsMappedList()
        {
            var expected = new List<SymptomModel> { new() { Id = SymptomId, EventName = EventName } };
            SetupFindAsync(new[] { ValidEntity() });
            SetupListMapping(expected);
            var service = CreateService();

            var result = await service.GetByDateAsync(UserId, EventDate);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GetByDateAsync_PredicateMatchesSameDateIgnoringTimeAndOwner()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            await service.GetByDateAsync(UserId, EventDate);

            Assert.NotNull(_capturedPredicate);
            var predicate = _capturedPredicate!.Compile();

            Assert.True(predicate(new Symptom { UserId = UserId, EventDate = EventDate }));
            // La normalizzazione .Date deve ignorare l'ora.
            Assert.True(predicate(new Symptom { UserId = UserId, EventDate = EventDate.AddHours(5) }));
            Assert.False(predicate(new Symptom { UserId = UserId, EventDate = EventDate.AddDays(1) }));
            Assert.False(predicate(new Symptom { UserId = OtherUserId, EventDate = EventDate }));
        }

        // --- GetByNameAsync ---

        [Fact]
        public async Task GetByNameAsync_ReturnsMappedList()
        {
            var expected = new List<SymptomModel> { new() { Id = SymptomId, EventName = EventName } };
            SetupFindAsync(new[] { ValidEntity() });
            SetupListMapping(expected);
            var service = CreateService();

            var result = await service.GetByNameAsync(UserId, EventName);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GetByNameAsync_PredicateMatchesExactNameAndOwner()
        {
            SetupFindAsync(NoSymptoms);
            var service = CreateService();

            await service.GetByNameAsync(UserId, EventName);

            Assert.NotNull(_capturedPredicate);
            var predicate = _capturedPredicate!.Compile();

            Assert.True(predicate(new Symptom { UserId = UserId, EventName = EventName }));
            Assert.False(predicate(new Symptom { UserId = UserId, EventName = "tosse" }));
            Assert.False(predicate(new Symptom { UserId = OtherUserId, EventName = EventName }));
        }
    }
}