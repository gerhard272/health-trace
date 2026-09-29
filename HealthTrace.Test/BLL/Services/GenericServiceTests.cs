using AutoMapper;
using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;

namespace HealthTrace.Test.BLL.Services
{
    /// <summary>
    /// Test di GenericService&lt;TEntity, TModel&gt;, la base generica da cui eredita
    /// UserService. Il CRUD che eredita era privo di copertura: i test esistenti
    /// riguardano solo RegisterAsync e LoginAsync, cioe' i metodi aggiunti dalla classe
    /// derivata, mai i cinque ereditati.
    /// I test corrono su tipi fittizi con IMapper mockato: nessun DbContext, nessun
    /// profilo di mapping reale, nessuna dipendenza dallo stato dell'applicazione, e
    /// quindi la suite resta eseguibile in qualunque momento.
    /// Nota sui due tipi locali: sono public perche' Castle DynamicProxy genera i
    /// proxy in un assembly dinamico, che non puo' riferire tipi annidati non pubblici.
    /// Con private o internal il fallimento sarebbe un TypeLoadException senza alcun
    /// rapporto con la logica sotto test.
    /// Nota sulle quattro firme di IMapper: vanno registrate separatamente, ognuna
    /// con il proprio argomento generico, altrimenti i setup si sovrascrivono a vicenda.
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
            // Il repository non e' iniettato: viene risolto dal UnitOfWork dentro il
            // costruttore. Per questo CreateService() va SEMPRE chiamato per primo e i
            // setup che servono al test vanno messi DOPO, altrimenti vengono scartati.
            _unitOfWork.Setup(u => u.Repository<TestEntity>()).Returns(_repository.Object);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            return new GenericService<TestEntity, TestModel>(_unitOfWork.Object, _mapper.Object);
        }

        // --- Costruzione ---

        [Fact]
        public void Constructor_ResolvesRepositoryFromUnitOfWork()
        {
            CreateService();

            // La risoluzione avviene una volta sola, in costruzione: se cambiasse o
            // venisse rimossa, ogni metodo si troverebbe a parlare con un repository
            // sbagliato o nullo, e fallirebbe piu' a valle senza indizi utili.
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
            // Il ramo null deve cortocircuitare prima del mapper: con un mapper vero,
            // Map<TestModel>(null) non avrebbe nulla su cui lavorare.
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
            // Al mapper arriva il risultato del repository cosi' com'e': questo service
            // non filtra ne' riordina, e un test che lo assumesse fallirebbe qui.
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

            // Elenco vuoto e' un caso normale, non un errore: deve restare una lista
            // vuota e non un null, altrimenti il controller dovrebbe proteggerlo.
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

            // Salvare prima di accodare non persisterebbe nulla: l'entita' non sarebbe
            // ancora tracciata e la riga non arriverebbe mai in tabella.
            Assert.Equal(new[] { "Add", "Save" }, calls);
        }

        [Fact]
        public async Task CreateAsync_ValidModel_ReturnsModelMappedFromPersistedEntity()
        {
            var service = CreateService();
            _mapper
                .Setup(m => m.Map<TestEntity>(It.IsAny<TestModel>()))
                .Returns(new TestEntity { Name = "febbre" });
            // L'Id lo assegna il database, non il model in ingresso: qui lo simuliamo
            // dentro AddAsync, che e' il momento in cui avviene davvero.
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

            // Il ritorno e' mappato dall'entita' DOPO AddAsync. Se il mapping avvenisse
            // prima, l'Id resterebbe 0 e il chiamante riceverebbe un model inesistente:
            // un bug che nessun test sui soli valori di ingresso riuscirebbe a vedere.
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
            // L'uscita anticipata e' tutto il punto di questo test: senza, un model
            // riferito a una riga inesistente produrrebbe un update a vuoto.
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

            // La ricerca usa model.Id: e' l'unico modo che il chiamante ha di dire
            // quale riga aggiornare, e il service non deve indovinarlo.
            _repository.Verify(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
            // I dati finiscono SULL'entita' esistente e non su una nuova: e' cio' che
            // preserva le colonne non mappate e l'identita' della riga.
            _mapper.Verify(m => m.Map<TestModel, TestEntity>(model, existing), Times.Once);
            _repository.Verify(r => r.Update(existing), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ExistingEntity_ReturnsModelMappedFromExistingEntity()
        {
            var model = new TestModel { Id = 5, Name = "dal model in ingresso" };
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

            // L'argomento del mapping di ritorno e' l'entita' esistente, non il model
            // in ingresso. Se qualcuno scrivesse Map<TestModel>(model), il chiamante
            // riceverebbe i dati richiesti prima che siano stati applicati e salvati.
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

            // false e non un'eccezione: il chiamante distingue cosi' "non esisteva" da
            // "cancellato", che sono esiti diversi per lo stesso tipo di risposta.
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
