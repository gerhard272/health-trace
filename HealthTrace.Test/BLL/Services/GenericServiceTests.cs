using AutoMapper;
using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;

namespace HealthTrace.Test.BLL.Services
{
    /// <summary>
    /// Tests for GenericService&lt;TEntity, TModel&gt;, the generic base that
    /// UserService inherits from. The inherited CRUD had no coverage: the existing tests
    /// only cover RegisterAsync and LoginAsync, i.e. the methods added by the derived
    /// class, never the five inherited ones.
    /// The tests run on fake types with a mocked IMapper: no DbContext, no
    /// real mapping profile, no dependency on application state, and
    /// so the suite can always be run.
    /// Note on the two local types: they are public because Castle DynamicProxy generates
    /// proxies in a dynamic assembly, which cannot reference non-public nested types.
    /// With private or internal the failure would be a TypeLoadException unrelated
    /// to the logic under test.
    /// Note on the four IMapper signatures: they must be set up separately, each
    /// with its own generic argument, otherwise the setups override each other.
    /// </summary>
    public class GenericServiceTests
    {
        public class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class TestModel : IModelWithId
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGenericRepository<TestEntity>> _repository = new();
        private readonly Mock<IMapper> _mapper = new();

        private static readonly IReadOnlyList<TestEntity> NoEntities = Array.Empty<TestEntity>();
        private static readonly IReadOnlyList<TestModel> NoModels = Array.Empty<TestModel>();

        private GenericService<TestEntity, TestModel> CreateService()
        {
            // The repository is not injected: it is resolved from the UnitOfWork inside the
            // constructor. That is why CreateService() must ALWAYS be called first and the
            // setups the test needs go AFTER it, otherwise they are discarded.
            _unitOfWork.Setup(u => u.Repository<TestEntity>()).Returns(_repository.Object);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            return new GenericService<TestEntity, TestModel>(_unitOfWork.Object, _mapper.Object);
        }

        // --- Construction ---

        [Fact]
        public void Constructor_ResolvesRepositoryFromUnitOfWork()
        {
            CreateService();

            // Resolution happens only once, at construction: if it changed or
            // were removed, every method would talk to a wrong or null
            // repository and fail further downstream without useful clues.
            _unitOfWork.Verify(u => u.Repository<TestEntity>(), Times.Once);
        }

        // --- GetByIdAsync ---

        [Fact]
        public async Task GetByIdAsync_ExistingEntity_ReturnsMappedModel()
        {
            var entity = new TestEntity { Id = 7, Name = "febbre" };
            var model = new TestModel { Id = 7, Name = "febbre" };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            _mapper.Setup(m => m.Map<TestModel>(It.IsAny<object>())).Returns(model);

            var result = await service.GetByIdAsync(7);

            Assert.Same(model, result);
            _repository.Verify(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_UnknownId_ReturnsNullWithoutMapping()
        {
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestEntity?)null);

            var result = await service.GetByIdAsync(404);

            Assert.Null(result);
            // The null branch must short-circuit before the mapper: with a real mapper,
            // Map<TestModel>(null) would have nothing to work on.
            _mapper.Verify(m => m.Map<TestModel>(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ValidId_PropagatesCancellationToken()
        {
            CancellationToken? getToken = null;
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, CancellationToken>((_, ct) => getToken = ct)
                .ReturnsAsync((TestEntity?)null);
            using var cts = new CancellationTokenSource();

            await service.GetByIdAsync(1, cts.Token);

            Assert.Equal(cts.Token, getToken);
        }

        // --- GetAllAsync ---

        [Fact]
        public async Task GetAllAsync_AnyEntities_MapsRepositoryResult()
        {
            var entities = new[]
            {
                new TestEntity { Id = 1, Name = "a" },
                new TestEntity { Id = 2, Name = "b" }
            };
            var models = new[]
            {
                new TestModel { Id = 1, Name = "a" },
                new TestModel { Id = 2, Name = "b" }
            };
            var service = CreateService();
            _repository
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(entities);

            object? mappedSource = null;
            _mapper
                .Setup(m => m.Map<IReadOnlyList<TestModel>>(It.IsAny<object>()))
                .Callback<object>(source => mappedSource = source)
                .Returns(models);

            var result = await service.GetAllAsync();

            Assert.Equal(new List<int> { 1, 2 }, result.Select(m => m.Id).ToList());
            // The mapper receives the repository result as is: this service
            // neither filters nor sorts, and a test assuming it would fail here.
            Assert.Same(entities, mappedSource);
        }

        [Fact]
        public async Task GetAllAsync_NoEntities_ReturnsEmptyList()
        {
            var service = CreateService();
            _repository
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(NoEntities);
            _mapper
                .Setup(m => m.Map<IReadOnlyList<TestModel>>(It.IsAny<object>()))
                .Returns(NoModels);

            var result = await service.GetAllAsync();

            // An empty list is a normal case, not an error: it must stay an empty
            // list and not null, otherwise the controller would have to guard against it.
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // --- CreateAsync ---

        [Fact]
        public async Task CreateAsync_ValidModel_AddsBeforeSaving()
        {
            var calls = new List<string>();
            var service = CreateService();
            _mapper
                .Setup(m => m.Map<TestEntity>(It.IsAny<TestModel>()))
                .Returns(new TestEntity { Name = "febbre" });
            _repository
                .Setup(r => r.AddAsync(It.IsAny<TestEntity>(), It.IsAny<CancellationToken>()))
                .Callback<TestEntity, CancellationToken>((_, _) => calls.Add("Add"))
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ => calls.Add("Save"))
                .ReturnsAsync(1);

            await service.CreateAsync(new TestModel { Name = "febbre" });

            // Saving before adding would persist nothing: the entity would not
            // be tracked yet and the row would never reach the table.
            Assert.Equal(new[] { "Add", "Save" }, calls);
        }

        [Fact]
        public async Task CreateAsync_ValidModel_ReturnsModelMappedFromPersistedEntity()
        {
            var service = CreateService();
            _mapper
                .Setup(m => m.Map<TestEntity>(It.IsAny<TestModel>()))
                .Returns(new TestEntity { Name = "febbre" });
            // The Id is assigned by the database, not by the incoming model: we simulate it
            // inside AddAsync, which is when it really happens.
            _repository
                .Setup(r => r.AddAsync(It.IsAny<TestEntity>(), It.IsAny<CancellationToken>()))
                .Callback<TestEntity, CancellationToken>((entity, _) => entity.Id = 42)
                .Returns(Task.CompletedTask);
            _mapper
                .Setup(m => m.Map<TestModel>(It.IsAny<object>()))
                .Returns((object source) =>
                {
                    var entity = (TestEntity)source;
                    return new TestModel { Id = entity.Id, Name = entity.Name };
                });

            var result = await service.CreateAsync(new TestModel { Name = "febbre" });

            // The return value is mapped from the entity AFTER AddAsync. If the mapping happened
            // before, the Id would stay 0 and the caller would receive a non-existent model:
            // a bug that no test on input values alone could catch.
            Assert.Equal(42, result.Id);
            Assert.Equal("febbre", result.Name);
        }

        [Fact]
        public async Task CreateAsync_ValidModel_PropagatesCancellationToken()
        {
            CancellationToken? addToken = null, saveToken = null;
            var service = CreateService();
            _mapper
                .Setup(m => m.Map<TestEntity>(It.IsAny<TestModel>()))
                .Returns(new TestEntity());
            _mapper
                .Setup(m => m.Map<TestModel>(It.IsAny<object>()))
                .Returns(new TestModel());
            _repository
                .Setup(r => r.AddAsync(It.IsAny<TestEntity>(), It.IsAny<CancellationToken>()))
                .Callback<TestEntity, CancellationToken>((_, ct) => addToken = ct)
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            using var cts = new CancellationTokenSource();

            await service.CreateAsync(new TestModel { Name = "febbre" }, cts.Token);

            Assert.Equal(cts.Token, addToken);
            Assert.Equal(cts.Token, saveToken);
        }

        // --- UpdateAsync ---

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNullWithoutUpdatingOrSaving()
        {
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestEntity?)null);

            var result = await service.UpdateAsync(new TestModel { Id = 5, Name = "febbre" });

            Assert.Null(result);
            // The early exit is the whole point of this test: without it, a model
            // pointing to a non-existent row would produce an empty update.
            _repository.Verify(r => r.Update(It.IsAny<TestEntity>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mapper.Verify(m => m.Map<TestModel, TestEntity>(
                It.IsAny<TestModel>(), It.IsAny<TestEntity>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ExistingEntity_MapsOntoExistingAndPersists()
        {
            var model = new TestModel { Id = 5, Name = "febbre aggiornata" };
            var existing = new TestEntity { Id = 5, Name = "febbre" };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mapper
                .Setup(m => m.Map<TestModel, TestEntity>(It.IsAny<TestModel>(), It.IsAny<TestEntity>()))
                .Returns(existing);
            _mapper
                .Setup(m => m.Map<TestModel>(It.IsAny<object>()))
                .Returns(model);

            await service.UpdateAsync(model);

            // The lookup uses model.Id: it is the only way the caller has to say
            // which row to update, and the service must not guess it.
            _repository.Verify(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
            // The data lands ON the existing entity and not on a new one: that is what
            // preserves unmapped columns and the row's identity.
            _mapper.Verify(m => m.Map<TestModel, TestEntity>(model, existing), Times.Once);
            _repository.Verify(r => r.Update(existing), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ExistingEntity_ReturnsModelMappedFromExistingEntity()
        {
            var model = new TestModel { Id = 5, Name = "from the incoming model" };
            var existing = new TestEntity { Id = 5, Name = "dall'entita' esistente" };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mapper
                .Setup(m => m.Map<TestModel, TestEntity>(It.IsAny<TestModel>(), It.IsAny<TestEntity>()))
                .Returns(existing);

            TestEntity? returnedFrom = null;
            _mapper
                .Setup(m => m.Map<TestModel>(It.IsAny<object>()))
                .Callback<object>(source => returnedFrom = (TestEntity)source)
                .Returns(new TestModel { Id = 5, Name = "risultato" });

            await service.UpdateAsync(model);

            // The argument of the return mapping is the existing entity, not the incoming
            // model. If someone wrote Map<TestModel>(model), the caller
            // would receive the requested data before it was applied and saved.
            Assert.Same(existing, returnedFrom);
        }

        [Fact]
        public async Task UpdateAsync_ExistingEntity_PropagatesCancellationToken()
        {
            CancellationToken? getToken = null, saveToken = null;
            var existing = new TestEntity { Id = 5 };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, CancellationToken>((_, ct) => getToken = ct)
                .ReturnsAsync(existing);
            _mapper
                .Setup(m => m.Map<TestModel, TestEntity>(It.IsAny<TestModel>(), It.IsAny<TestEntity>()))
                .Returns(existing);
            _mapper
                .Setup(m => m.Map<TestModel>(It.IsAny<object>()))
                .Returns(new TestModel { Id = 5 });
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            using var cts = new CancellationTokenSource();

            await service.UpdateAsync(new TestModel { Id = 5 }, cts.Token);

            Assert.Equal(cts.Token, getToken);
            Assert.Equal(cts.Token, saveToken);
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_UnknownId_ReturnsFalseWithoutDeletingOrSaving()
        {
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestEntity?)null);

            var result = await service.DeleteAsync(404);

            // false and not an exception: this way the caller tells "did not exist" apart from
            // "deleted", which are different outcomes for the same kind of response.
            Assert.False(result);
            _repository.Verify(r => r.Delete(It.IsAny<TestEntity>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ExistingEntity_DeletesAndReturnsTrue()
        {
            var existing = new TestEntity { Id = 5 };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var result = await service.DeleteAsync(5);

            Assert.True(result);
            _repository.Verify(r => r.Delete(existing), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ExistingEntity_PropagatesCancellationToken()
        {
            CancellationToken? getToken = null, saveToken = null;
            var existing = new TestEntity { Id = 5 };
            var service = CreateService();
            _repository
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, CancellationToken>((_, ct) => getToken = ct)
                .ReturnsAsync(existing);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            using var cts = new CancellationTokenSource();

            await service.DeleteAsync(5, cts.Token);

            Assert.Equal(cts.Token, getToken);
            Assert.Equal(cts.Token, saveToken);
        }
    }
}
