using System.Reflection;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
// Il namespace di questo file replica quello del codice sotto test, quindi la
// regola di risoluzione dei namespace annidati non trova da sola il controller:
// la dichiarazione esplicita serve.
using HealthTrace.PL.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;

namespace HealthTrace.Test.PL.API.Controllers
{
    /// <summary>
    /// Classe di test per SymptomController. A differenza di AuthController, qui il
    /// controller contiene logica propria: ricava l'utente autenticato da
    /// ICurrentUserService, sceglie il metodo del servizio in base ai filtri della
    /// query, controlla la coerenza fra id di rotta e id del body e traduce i
    /// risultati nulli o false del servizio in NotFoundException.
    /// Gli esiti negativi non producono ActionResult: il controller lancia
    /// l'eccezione applicativa, che il gestore globale traduce in ProblemDetails.
    /// Per questo i test asseriscono tipo e contenuto dell'eccezione lanciata.
    /// </summary>
    public class SymptomControllerTests
    {
        private const int UserId = 7;
        private const int SymptomId = 99;
        private const string ResourceName = "Symptom";
        private const string EventName = "febbre";

        private static readonly DateTime EventDate = new(2026, 9, 23);

        private readonly Mock<ISymptomService> _service = new();
        private readonly Mock<ICurrentUserService> _currentUser = new();

        private SymptomController CreateController(int? userId = UserId)
        {
            _currentUser.Setup(c => c.UserId).Returns(userId);
            return new SymptomController(_service.Object, _currentUser.Object);
        }

        private static SymptomModel Symptom(int id = SymptomId) => new()
        {
            Id = id,
            UserId = UserId,
            EventName = EventName,
            Description = "38.5 gradi",
            EventDate = EventDate
        };

        // --- Utente non autenticato ---

        public static TheoryData<string, Func<SymptomController, Task>> AllActions => new()
        {
            { nameof(SymptomController.GetById), c => c.GetById(SymptomId, CancellationToken.None) },
            { nameof(SymptomController.GetAllSymptoms), c => c.GetAllSymptoms(null, null, CancellationToken.None) },
            { nameof(SymptomController.Create), c => c.Create(Symptom(), CancellationToken.None) },
            { nameof(SymptomController.Update), c => c.Update(SymptomId, Symptom(), CancellationToken.None) },
            { nameof(SymptomController.Delete), c => c.Delete(SymptomId, CancellationToken.None) }
        };

        [Theory]
        [MemberData(nameof(AllActions))]
        public async Task Action_WithoutAuthenticatedUser_ThrowsUnauthorizedException(
            string actionName, Func<SymptomController, Task> invoke)
        {
            var controller = CreateController(userId: null);

            await Assert.ThrowsAsync<UnauthorizedException>(() => invoke(controller));

            // Senza utente il controllo avviene prima di qualsiasi accesso ai dati:
            // il servizio non deve essere mai raggiunto, qualunque sia l'action.
            _service.VerifyNoOtherCalls();

            // actionName serve solo a rendere leggibile il nome del caso nel test explorer.
            _ = actionName;
        }

        // --- GetById ---

        [Fact]
        public async Task GetById_ExistingSymptom_Returns200OkWithSymptom()
        {
            var symptom = Symptom();
            _service.Setup(s => s.GetByIdAsync(UserId, SymptomId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(symptom);
            var controller = CreateController();

            var result = await controller.GetById(SymptomId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.Same(symptom, ok.Value);
        }

        [Fact]
        public async Task GetById_PassesUserIdAndCancellationTokenToService()
        {
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetByIdAsync(UserId, SymptomId, cts.Token)).ReturnsAsync(Symptom());
            var controller = CreateController();

            await controller.GetById(SymptomId, cts.Token);

            // L'utente è sempre quello autenticato: è il servizio a filtrare per
            // proprietario, quindi l'id utente corretto è ciò che impedisce di
            // leggere i sintomi di un altro utente.
            _service.Verify(s => s.GetByIdAsync(UserId, SymptomId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetById_SymptomNotFound_ThrowsNotFoundException()
        {
            _service.Setup(s => s.GetByIdAsync(UserId, SymptomId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SymptomModel?)null);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.GetById(SymptomId, CancellationToken.None));

            // Nome risorsa e chiave alimentano le estensioni del 404 nel mapper.
            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(SymptomId, ex.Key);
        }

        // --- GetAllSymptoms ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task GetAllSymptoms_WithoutFilters_ReturnsAllUserSymptoms(string? name)
        {
            // Un nome vuoto o fatto di soli spazi equivale all'assenza del filtro.
            IReadOnlyList<SymptomModel> symptoms = [Symptom(1), Symptom(2)];
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetAllByUserIdAsync(UserId, cts.Token)).ReturnsAsync(symptoms);
            var controller = CreateController();

            var result = await controller.GetAllSymptoms(null, name, cts.Token);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(symptoms, ok.Value);

            _service.Verify(s => s.GetAllByUserIdAsync(UserId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAllSymptoms_WithDate_ReturnsSymptomsByDate()
        {
            IReadOnlyList<SymptomModel> symptoms = [Symptom()];
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetByDateAsync(UserId, EventDate, cts.Token)).ReturnsAsync(symptoms);
            var controller = CreateController();

            var result = await controller.GetAllSymptoms(EventDate, null, cts.Token);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(symptoms, ok.Value);

            _service.Verify(s => s.GetByDateAsync(UserId, EventDate, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAllSymptoms_WithName_ReturnsSymptomsByName()
        {
            IReadOnlyList<SymptomModel> symptoms = [Symptom()];
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetByNameAsync(UserId, EventName, cts.Token)).ReturnsAsync(symptoms);
            var controller = CreateController();

            var result = await controller.GetAllSymptoms(null, EventName, cts.Token);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(symptoms, ok.Value);

            _service.Verify(s => s.GetByNameAsync(UserId, EventName, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAllSymptoms_WithDateAndName_ThrowsBadRequestException()
        {
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<BadRequestException>(
                () => controller.GetAllSymptoms(EventDate, EventName, CancellationToken.None));

            Assert.Equal("Specify only one of 'date' or 'name'.", ex.Message);

            // I filtri sono mutuamente esclusivi: la richiesta viene respinta senza
            // interrogare il servizio, invece di applicarne uno solo in silenzio.
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAllSymptoms_WithDateAndBlankName_FiltersByDate()
        {
            // Il nome fatto di soli spazi non conta come filtro: non deve far scattare
            // l'errore dei filtri combinati.
            IReadOnlyList<SymptomModel> symptoms = [Symptom()];
            _service.Setup(s => s.GetByDateAsync(UserId, EventDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(symptoms);
            var controller = CreateController();

            var result = await controller.GetAllSymptoms(EventDate, " ", CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Same(symptoms, ok.Value);
            _service.Verify(s => s.GetByNameAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // --- Create ---

        [Fact]
        public async Task Create_ValidSymptom_Returns201CreatedAtGetById()
        {
            var input = Symptom(id: 0);
            var created = Symptom();
            _service.Setup(s => s.CreateAsync(UserId, input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(created);
            var controller = CreateController();

            var result = await controller.Create(input, CancellationToken.None);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

            // L'header Location punta a GetById con l'id assegnato dal servizio,
            // non con quello (assente) del body ricevuto.
            Assert.Equal(nameof(SymptomController.GetById), createdResult.ActionName);
            Assert.Equal(SymptomId, createdResult.RouteValues!["id"]);
            Assert.Same(created, createdResult.Value);
        }

        [Fact]
        public async Task Create_PassesAuthenticatedUserIdIgnoringBodyUserId()
        {
            // Il body dichiara un altro proprietario: il controller deve comunque
            // passare al servizio l'id dell'utente autenticato.
            var input = Symptom(id: 0);
            input.UserId = 12345;
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.CreateAsync(UserId, input, cts.Token)).ReturnsAsync(Symptom());
            var controller = CreateController();

            await controller.Create(input, cts.Token);

            _service.Verify(s => s.CreateAsync(UserId, input, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        // --- Update ---

        [Fact]
        public async Task Update_ExistingSymptom_Returns204NoContent()
        {
            var symptom = Symptom();
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.UpdateAsync(UserId, symptom, cts.Token)).ReturnsAsync(symptom);
            var controller = CreateController();

            var result = await controller.Update(SymptomId, symptom, cts.Token);

            var noContent = Assert.IsType<NoContentResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

            _service.Verify(s => s.UpdateAsync(UserId, symptom, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Update_RouteIdDiffersFromBodyId_ThrowsBadRequestException()
        {
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<BadRequestException>(
                () => controller.Update(SymptomId + 1, Symptom(), CancellationToken.None));

            Assert.Equal("The id in the route does not match the id in the request body.", ex.Message);

            // L'incoerenza viene respinta prima di toccare i dati.
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Update_SymptomNotFound_ThrowsNotFoundException()
        {
            _service.Setup(s => s.UpdateAsync(UserId, It.IsAny<SymptomModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SymptomModel?)null);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.Update(SymptomId, Symptom(), CancellationToken.None));

            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(SymptomId, ex.Key);
        }

        // --- Delete ---

        [Fact]
        public async Task Delete_ExistingSymptom_Returns204NoContent()
        {
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.DeleteAsync(UserId, SymptomId, cts.Token)).ReturnsAsync(true);
            var controller = CreateController();

            var result = await controller.Delete(SymptomId, cts.Token);

            var noContent = Assert.IsType<NoContentResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

            _service.Verify(s => s.DeleteAsync(UserId, SymptomId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Delete_SymptomNotFound_ThrowsNotFoundException()
        {
            _service.Setup(s => s.DeleteAsync(UserId, SymptomId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.Delete(SymptomId, CancellationToken.None));

            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(SymptomId, ex.Key);
        }

        // --- Attributi di routing e autorizzazione ---

        [Fact]
        public void Controller_RequiresAuthorizationAndUsesSymptomRoute()
        {
            var type = typeof(SymptomController);

            // A differenza di AuthController, tutte le action richiedono un utente
            // autenticato e nessuna deve sfuggire con AllowAnonymous.
            Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/[controller]", type.GetCustomAttribute<RouteAttribute>()?.Template);

            Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.All(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
        }

        [Theory]
        [InlineData(nameof(SymptomController.GetById), typeof(HttpGetAttribute), "{id:int}")]
        [InlineData(nameof(SymptomController.GetAllSymptoms), typeof(HttpGetAttribute), null)]
        [InlineData(nameof(SymptomController.Create), typeof(HttpPostAttribute), null)]
        [InlineData(nameof(SymptomController.Update), typeof(HttpPutAttribute), "{id:int}")]
        [InlineData(nameof(SymptomController.Delete), typeof(HttpDeleteAttribute), "{id:int}")]
        public void Action_HasExpectedHttpMethodAndTemplate(
            string actionName, Type attributeType, string? expectedTemplate)
        {
            var method = typeof(SymptomController).GetMethod(actionName)!;

            var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());

            Assert.IsType(attributeType, attribute);
            Assert.Equal(expectedTemplate, attribute.Template);
        }
    }
}
