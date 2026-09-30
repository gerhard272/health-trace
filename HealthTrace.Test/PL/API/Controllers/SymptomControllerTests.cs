using System.Reflection;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
// This file's namespace mirrors that of the code under test, so the
// nested namespace resolution rule does not find the controller on its own:
// the explicit declaration is needed.
using HealthTrace.PL.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;

namespace HealthTrace.Test.PL.API.Controllers
{
    /// <summary>
    /// Test class for SymptomController. Unlike AuthController, this
    /// controller has logic of its own: it gets the authenticated user from
    /// ICurrentUserService, picks the service method based on the query
    /// filters, checks that the route id matches the body id and turns
    /// null or false service results into NotFoundException.
    /// Failures do not produce an ActionResult: the controller throws
    /// the application exception, which the global handler turns into ProblemDetails.
    /// That is why the tests assert the type and content of the thrown exception.
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

        // --- Unauthenticated user ---

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

            // Without a user the check happens before any data access:
            // the service must never be reached, whatever the action.
            _service.VerifyNoOtherCalls();

            // actionName only makes the case name readable in the test explorer.
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

            // The user is always the authenticated one: the service filters by
            // owner, so the correct user id is what prevents
            // reading another user's symptoms.
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

            // Resource name and key feed the 404 extensions in the mapper.
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
            // An empty or whitespace-only name is the same as no filter.
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

            // The filters are mutually exclusive: the request is rejected without
            // querying the service, instead of silently applying only one.
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAllSymptoms_WithDateAndBlankName_FiltersByDate()
        {
            // A whitespace-only name does not count as a filter: it must not trigger
            // the combined-filters error.
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

            // The Location header points to GetById with the id assigned by the service,
            // not with the (missing) one from the received body.
            Assert.Equal(nameof(SymptomController.GetById), createdResult.ActionName);
            Assert.Equal(SymptomId, createdResult.RouteValues!["id"]);
            Assert.Same(created, createdResult.Value);
        }

        [Fact]
        public async Task Create_PassesAuthenticatedUserIdIgnoringBodyUserId()
        {
            // The body declares a different owner: the controller must still
            // pass the authenticated user's id to the service.
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

            // The mismatch is rejected before touching the data.
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

        // --- Routing and authorization attributes ---

        [Fact]
        public void Controller_RequiresAuthorizationAndUsesSymptomRoute()
        {
            var type = typeof(SymptomController);

            // Unlike AuthController, every action requires an authenticated
            // user and none may slip through with AllowAnonymous.
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
