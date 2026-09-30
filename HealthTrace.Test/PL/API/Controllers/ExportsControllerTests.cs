using System.Reflection;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
using HealthTrace.DAL.Entities;
using HealthTrace.PL.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;

namespace HealthTrace.Test.PL.API.Controllers
{
    /// <summary>
    /// Classe di test per ExportsController. Il controller ricava l'utente autenticato
    /// da ICurrentUserService, valida l'intervallo di date della richiesta, affida
    /// l'elaborazione a IExportJobDispatcher e traduce gli esiti del servizio in
    /// risposte HTTP. Come negli altri controller, gli esiti negativi non producono
    /// ActionResult: il controller lancia l'eccezione applicativa, che il gestore
    /// globale traduce in ProblemDetails. I test asseriscono quindi tipo e contenuto
    /// dell'eccezione, e gli attributi ProducesResponseType che documentano quegli
    /// status code nella specifica OpenAPI.
    /// </summary>
    public class ExportsControllerTests
    {
        private const int UserId = 7;
        private const int ExportId = 99;
        private const string ResourceName = "Export";
        private const string FileName = "symptoms-20260923-120000.pdf";
        private const string PdfContentType = "application/pdf";

        private static readonly DateTime FromDate = new(2026, 9, 1);
        private static readonly DateTime ToDate = new(2026, 9, 30);

        private readonly Mock<IExportService> _service = new();
        private readonly Mock<IExportJobDispatcher> _dispatcher = new();
        private readonly Mock<ICurrentUserService> _currentUser = new();

        private ExportsController CreateController(int? userId = UserId)
        {
            _currentUser.Setup(c => c.UserId).Returns(userId);
            return new ExportsController(_service.Object, _dispatcher.Object, _currentUser.Object);
        }

        private static ExportRequestCreateModel CreateModel(
            DateTime? from = null, DateTime? to = null) => new()
        {
            SymptomIds = [1, 2],
            FromDate = from,
            ToDate = to
        };

        private static ExportRequestModel Export(
            ExportStatus status = ExportStatus.Pending, string? errorMessage = null) => new()
        {
            Id = ExportId,
            Status = status,
            CreatedAt = new DateTime(2026, 9, 23, 12, 0, 0),
            SymptomIds = [1, 2],
            FileName = status == ExportStatus.Completed ? FileName : null,
            ErrorMessage = errorMessage
        };

        private void SetupRequestExport(ExportRequestModel created, ExportRequestModel? current)
        {
            _service
                .Setup(s => s.RequestExportAsync(UserId, It.IsAny<ExportRequestCreateModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(created);
            _service
                .Setup(s => s.GetByIdAsync(UserId, created.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(current);
        }

        // --- Utente non autenticato ---

        public static TheoryData<string, Func<ExportsController, Task>> AllActions => new()
        {
            { nameof(ExportsController.RequestExport), c => c.RequestExport(CreateModel(), CancellationToken.None) },
            { nameof(ExportsController.GetHistory), c => c.GetHistory(CancellationToken.None) },
            { nameof(ExportsController.GetById), c => c.GetById(ExportId, CancellationToken.None) },
            { nameof(ExportsController.Download), c => c.Download(ExportId, CancellationToken.None) }
        };

        [Theory]
        [MemberData(nameof(AllActions))]
        public async Task Action_WithoutAuthenticatedUser_ThrowsUnauthorizedException(
            string actionName, Func<ExportsController, Task> invoke)
        {
            var controller = CreateController(userId: null);

            await Assert.ThrowsAsync<UnauthorizedException>(() => invoke(controller));

            // Senza utente né il servizio né il dispatcher devono essere raggiunti.
            _service.VerifyNoOtherCalls();
            _dispatcher.VerifyNoOtherCalls();

            // actionName serve solo a rendere leggibile il nome del caso nel test explorer.
            _ = actionName;
        }

        [Fact]
        public async Task RequestExport_WithoutAuthenticatedUserAndInvalidRange_ThrowsUnauthorizedException()
        {
            // Il 401 ha la precedenza sul 400: a un client non autenticato non si
            // risponde con l'esito della validazione del body.
            var controller = CreateController(userId: null);

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => controller.RequestExport(CreateModel(from: ToDate, to: FromDate), CancellationToken.None));
        }

        // --- RequestExport ---

        [Fact]
        public async Task RequestExport_ValidModel_Returns202AcceptedAtGetByIdWithCurrentState()
        {
            var created = Export(ExportStatus.Pending);
            var current = Export(ExportStatus.Completed);
            SetupRequestExport(created, current);
            var controller = CreateController();

            var result = await controller.RequestExport(CreateModel(FromDate, ToDate), CancellationToken.None);

            var accepted = Assert.IsType<AcceptedAtActionResult>(result.Result);
            Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
            Assert.Equal(nameof(ExportsController.GetById), accepted.ActionName);
            Assert.Equal(ExportId, accepted.RouteValues!["id"]);

            // Il corpo è lo stato riletto dopo il dispatch, non quello della creazione:
            // con il dispatcher inline l'export può già essere completato.
            Assert.Same(current, accepted.Value);
        }

        [Fact]
        public async Task RequestExport_CreatesDispatchesAndRereadsInOrder()
        {
            var model = CreateModel();
            var created = Export();
            using var cts = new CancellationTokenSource();
            var calls = new List<string>();

            _service
                .Setup(s => s.RequestExportAsync(UserId, model, cts.Token))
                .Callback(() => calls.Add("request"))
                .ReturnsAsync(created);
            _dispatcher
                .Setup(d => d.DispatchAsync(ExportId, cts.Token))
                .Callback(() => calls.Add("dispatch"))
                .Returns(Task.CompletedTask);
            _service
                .Setup(s => s.GetByIdAsync(UserId, ExportId, cts.Token))
                .Callback(() => calls.Add("reread"))
                .ReturnsAsync(created);
            var controller = CreateController();

            await controller.RequestExport(model, cts.Token);

            // La rilettura ha senso solo dopo il dispatch, e il dispatch solo dopo
            // che la richiesta è stata salvata e ha un id.
            Assert.Equal(new[] { "request", "dispatch", "reread" }, calls);

            _service.Verify(s => s.RequestExportAsync(UserId, model, cts.Token), Times.Once);
            _dispatcher.Verify(d => d.DispatchAsync(ExportId, cts.Token), Times.Once);
            _service.Verify(s => s.GetByIdAsync(UserId, ExportId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
            _dispatcher.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RequestExport_RereadReturnsNull_Returns202WithCreatedModel()
        {
            // La richiesta è già stata registrata: se la rilettura non la trova il
            // client riceve comunque 202 con il modello creato, non un 404.
            var created = Export();
            SetupRequestExport(created, current: null);
            var controller = CreateController();

            var result = await controller.RequestExport(CreateModel(), CancellationToken.None);

            var accepted = Assert.IsType<AcceptedAtActionResult>(result.Result);
            Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
            Assert.Same(created, accepted.Value);
        }

        [Fact]
        public async Task RequestExport_FromDateAfterToDate_ThrowsBadRequestException()
        {
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<BadRequestException>(
                () => controller.RequestExport(CreateModel(from: ToDate, to: FromDate), CancellationToken.None));

            Assert.Equal("FromDate cannot be later than ToDate.", ex.Message);

            // L'intervallo non valido viene respinto prima di salvare o elaborare.
            _service.VerifyNoOtherCalls();
            _dispatcher.VerifyNoOtherCalls();
        }

        public static TheoryData<DateTime?, DateTime?> ValidRanges => new()
        {
            { null, null },
            { FromDate, null },
            { null, ToDate },
            { FromDate, ToDate },
            { FromDate, FromDate }
        };

        [Theory]
        [MemberData(nameof(ValidRanges))]
        public async Task RequestExport_ValidOrPartialRange_IsAccepted(DateTime? from, DateTime? to)
        {
            // Estremi mancanti o coincidenti non sono un errore: solo from > to lo è.
            SetupRequestExport(Export(), Export());
            var controller = CreateController();

            var result = await controller.RequestExport(CreateModel(from, to), CancellationToken.None);

            Assert.IsType<AcceptedAtActionResult>(result.Result);
        }

        // --- GetHistory ---

        [Fact]
        public async Task GetHistory_Returns200OkWithUserHistory()
        {
            IReadOnlyList<ExportRequestModel> history = [Export(ExportStatus.Completed), Export()];
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetHistoryAsync(UserId, cts.Token)).ReturnsAsync(history);
            var controller = CreateController();

            var result = await controller.GetHistory(cts.Token);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.Same(history, ok.Value);

            _service.Verify(s => s.GetHistoryAsync(UserId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        // --- GetById ---

        [Fact]
        public async Task GetById_ExistingExport_Returns200OkWithExport()
        {
            var export = Export();
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, cts.Token)).ReturnsAsync(export);
            var controller = CreateController();

            var result = await controller.GetById(ExportId, cts.Token);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.Same(export, ok.Value);

            _service.Verify(s => s.GetByIdAsync(UserId, ExportId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetById_ExportNotFound_ThrowsNotFoundException()
        {
            // Il servizio restituisce null anche per gli export di un altro utente:
            // il 404 non rivela se l'export esiste ma non è del chiamante.
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExportRequestModel?)null);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.GetById(ExportId, CancellationToken.None));

            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(ExportId, ex.Key);
        }

        // --- Download ---

        [Fact]
        public async Task Download_CompletedExport_ReturnsPdfFile()
        {
            using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
            using var cts = new CancellationTokenSource();
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, cts.Token))
                .ReturnsAsync(Export(ExportStatus.Completed));
            _service.Setup(s => s.GetFileAsync(UserId, ExportId, cts.Token))
                .ReturnsAsync(new ExportFileModel
                {
                    Content = content,
                    FileName = FileName,
                    ContentType = PdfContentType
                });
            var controller = CreateController();

            var result = await controller.Download(ExportId, cts.Token);

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Same(content, file.FileStream);
            Assert.Equal(PdfContentType, file.ContentType);

            // Il nome del file finisce nell'header Content-Disposition come allegato.
            Assert.Equal(FileName, file.FileDownloadName);

            _service.Verify(s => s.GetByIdAsync(UserId, ExportId, cts.Token), Times.Once);
            _service.Verify(s => s.GetFileAsync(UserId, ExportId, cts.Token), Times.Once);
            _service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Download_ExportNotFound_ThrowsNotFoundException()
        {
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExportRequestModel?)null);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.Download(ExportId, CancellationToken.None));

            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(ExportId, ex.Key);

            _service.Verify(s => s.GetFileAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(ExportStatus.Pending)]
        [InlineData(ExportStatus.Processing)]
        public async Task Download_ExportNotReady_ThrowsConflictExceptionWithStatus(ExportStatus status)
        {
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Export(status));
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<ConflictException>(
                () => controller.Download(ExportId, CancellationToken.None));

            // 409 e non 404: l'export esiste, ma il suo stato non consente ancora il download.
            Assert.Equal($"Export {ExportId} is not ready for download (status: {status}).", ex.Message);

            _service.Verify(s => s.GetFileAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Download_FailedExport_ThrowsConflictExceptionWithErrorMessage()
        {
            const string errorMessage = "PDF generation failed";
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Export(ExportStatus.Failed, errorMessage));
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<ConflictException>(
                () => controller.Download(ExportId, CancellationToken.None));

            // Per un export fallito il client riceve il motivo registrato dal servizio.
            Assert.Equal(errorMessage, ex.Message);

            _service.Verify(s => s.GetFileAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Download_CompletedExportWithoutFile_ThrowsNotFoundException()
        {
            // Stato Completed ma file assente (ad esempio blob non registrato):
            // il servizio restituisce null e il controller risponde 404.
            _service.Setup(s => s.GetByIdAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Export(ExportStatus.Completed));
            _service.Setup(s => s.GetFileAsync(UserId, ExportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExportFileModel?)null);
            var controller = CreateController();

            var ex = await Assert.ThrowsAsync<NotFoundException>(
                () => controller.Download(ExportId, CancellationToken.None));

            Assert.Equal(ResourceName, ex.ResourceName);
            Assert.Equal(ExportId, ex.Key);
        }

        // --- Attributi di routing e autorizzazione ---

        [Fact]
        public void Controller_RequiresAuthorizationAndUsesExportsRoute()
        {
            var type = typeof(ExportsController);

            Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/exports", type.GetCustomAttribute<RouteAttribute>()?.Template);

            Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.All(
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
        }

        [Theory]
        [InlineData(nameof(ExportsController.RequestExport), typeof(HttpPostAttribute), "request")]
        [InlineData(nameof(ExportsController.GetHistory), typeof(HttpGetAttribute), null)]
        [InlineData(nameof(ExportsController.GetById), typeof(HttpGetAttribute), "{id:int}")]
        [InlineData(nameof(ExportsController.Download), typeof(HttpGetAttribute), "{id:int}/download")]
        public void Action_HasExpectedHttpMethodAndTemplate(
            string actionName, Type attributeType, string? expectedTemplate)
        {
            var method = typeof(ExportsController).GetMethod(actionName)!;

            var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());

            Assert.IsType(attributeType, attribute);
            Assert.Equal(expectedTemplate, attribute.Template);
        }

        [Theory]
        [InlineData(nameof(ExportsController.RequestExport),
            new[] { StatusCodes.Status202Accepted, StatusCodes.Status400BadRequest,
                    StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError })]
        [InlineData(nameof(ExportsController.GetHistory),
            new[] { StatusCodes.Status200OK, StatusCodes.Status401Unauthorized,
                    StatusCodes.Status500InternalServerError })]
        [InlineData(nameof(ExportsController.GetById),
            new[] { StatusCodes.Status200OK, StatusCodes.Status401Unauthorized,
                    StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError })]
        [InlineData(nameof(ExportsController.Download),
            new[] { StatusCodes.Status200OK, StatusCodes.Status401Unauthorized,
                    StatusCodes.Status404NotFound, StatusCodes.Status409Conflict,
                    StatusCodes.Status500InternalServerError })]
        public void Action_DocumentsExpectedStatusCodes(string actionName, int[] expectedStatusCodes)
        {
            // Gli esiti negativi arrivano come eccezioni, quindi senza questi attributi
            // la specifica OpenAPI mostrerebbe solo la risposta di successo.
            var method = typeof(ExportsController).GetMethod(actionName)!;

            var documented = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Select(a => a.StatusCode)
                .Distinct()
                .Order()
                .ToArray();

            Assert.Equal(expectedStatusCodes.Order().ToArray(), documented);
        }

        [Theory]
        [InlineData(nameof(ExportsController.Download), StatusCodes.Status401Unauthorized)]
        [InlineData(nameof(ExportsController.Download), StatusCodes.Status404NotFound)]
        [InlineData(nameof(ExportsController.Download), StatusCodes.Status409Conflict)]
        [InlineData(nameof(ExportsController.GetById), StatusCodes.Status404NotFound)]
        [InlineData(nameof(ExportsController.RequestExport), StatusCodes.Status401Unauthorized)]
        public void Action_DocumentsErrorResponsesAsProblemDetails(string actionName, int statusCode)
        {
            var method = typeof(ExportsController).GetMethod(actionName)!;

            var attribute = Assert.Single(
                method.GetCustomAttributes<ProducesResponseTypeAttribute>(),
                a => a.StatusCode == statusCode);

            Assert.Equal(typeof(ProblemDetails), attribute.Type);
        }
    }
}
