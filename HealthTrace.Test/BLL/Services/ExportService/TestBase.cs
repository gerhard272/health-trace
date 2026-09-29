using System.Linq.Expressions;
using AutoMapper;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.DAL.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    // L'alias sta qui, dentro il corpo della namespace, e non in cima al file. Il nome
    // semplice "ExportService" e' anche il nome di questa namespace figlia di
    // ...BLL.Services, quindi la risoluzione ci arriva gia' legata alla namespace prima
    // di valutare gli using della compilation unit, che stanno piu' in fuori: un alias
    // messo in alto non verrebbe mai consultato e l'errore CS0118 ricomparirebbe.
    using ExportService = HealthTrace.BLL.Services.ExportService;
    /// <summary>
    /// Scaffolding condiviso dai test di ExportService, che e' il service piu' articolato
    /// del BLL: 5 metodi, 7 collaboratori e due repository risolti dal UnitOfWork dentro
    /// il suo costruttore. Ogni file di test eredita da qui i mock, le factory e gli
    /// helper di arrange, cos' i test si limitano al proprio scenario.
    /// La classe e' astratta e non contiene [Fact]: condivide il scaffolding, non i casi.
    /// </summary>
    public abstract class TestBase
    {
        protected const int UserId = 1;
        protected const int OtherUserId = 2;
        protected const int ExportRequestId = 100;
        protected const string ContainerName = "exports";
        protected const string PdfContentType = "application/pdf";
        protected const int MaxErrorLength = 1000;

        protected static readonly DateTime EventDate = new(2026, 9, 26, 10, 30, 0);
        protected static readonly IReadOnlyList<ExportRequest> NoExports = Array.Empty<ExportRequest>();
        protected static readonly IReadOnlyList<Symptom> NoSymptoms = Array.Empty<Symptom>();
        protected static readonly byte[] PdfBytes = { 1, 2, 3, 4 };

        protected readonly Mock<IUnitOfWork> _unitOfWork = new();
        protected readonly Mock<IGenericRepository<ExportRequest>> _exportRepository = new();
        protected readonly Mock<IGenericRepository<Symptom>> _symptomRepository = new();
        protected readonly Mock<IPdfGenerator> _pdfGenerator = new();
        protected readonly Mock<IBlobStorageService> _blobStorageService = new();
        protected readonly Mock<IMapper> _mapper = new();
        protected readonly Mock<ILogger<ExportService>> _logger = new();

        // ExportService chiama FindAsync su due repository diversi: la cronologia e la
        // richiesta di proprieta' passano da _exportRepository, la selezione dei sintomi
        // da _symptomRepository, quindi i predicati vanno catturati separatamente.
        protected Expression<Func<ExportRequest, bool>>? _capturedExportPredicate;
        protected Expression<Func<Symptom, bool>>? _capturedSymptomPredicate;
        protected CancellationToken? _capturedGetByIdToken;
        protected CancellationToken? _capturedSymptomToken;
        protected IReadOnlyList<Symptom>? _mappedSymptomSource;

        protected string? _uploadedContainer;
        protected string? _uploadedBlobName;
        protected string? _uploadedContentType;
        protected long _uploadedLength;
        protected string? _downloadedContainer;
        protected string? _downloadedBlobName;
        protected CancellationToken? _downloadedToken;
        protected ExportRequest? _addedEntity;

        // --- Factory ---

        protected static ExportRequestCreateModel CreateModel(
            List<int>? symptomIds = null,
            DateTime? fromDate = null,
            DateTime? toDate = null) => new()
        {
            SymptomIds = symptomIds,
            FromDate = fromDate,
            ToDate = toDate
        };

        protected static ExportRequest ValidEntity(
            int id = ExportRequestId,
            int userId = UserId,
            ExportStatus status = ExportStatus.Pending,
            List<int>? symptomIds = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? blobName = null,
            string? fileName = null,
            string? errorMessage = null,
            DateTime createdAt = default) => new()
        {
            Id = id,
            UserId = userId,
            Status = status,
            SymptomIds = symptomIds ?? new List<int>(),
            FromDate = fromDate,
            ToDate = toDate,
            BlobName = blobName,
            FileName = fileName,
            ErrorMessage = errorMessage,
            CreatedAt = createdAt
        };

        protected static Symptom ValidSymptom(int id, int userId = UserId, DateTime? eventDate = null) => new()
        {
            Id = id,
            UserId = userId,
            EventName = "febbre",
            EventDate = eventDate ?? EventDate
        };

        // --- Service ---

        protected ExportService CreateService()
        {
            // I due repository sono risolti dal UnitOfWork dentro il costruttore di
            // ExportService: se un setup manca, il repository arriva null al service
            // e fallisce piu' a valle, dentro ProcessExportAsync.
            _unitOfWork.Setup(u => u.Repository<ExportRequest>()).Returns(_exportRepository.Object);
            _unitOfWork.Setup(u => u.Repository<Symptom>()).Returns(_symptomRepository.Object);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // AddAsync e' chiamato solo da RequestExportAsync: la cattura qui evita che
            // ogni test debba risettare il mock solo per ispezionare l'entita' accodata.
            // Nota bene: e' una setup che i test possono sovrascrivere a piacere, perche'
            // CreateService() va comunque chiamato per primo, non per ultimo.
            _addedEntity = null;
            _exportRepository
                .Setup(r => r.AddAsync(It.IsAny<ExportRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRequest, CancellationToken>((entity, _) => _addedEntity = entity)
                .Returns(Task.CompletedTask);

            return new ExportService(
                _unitOfWork.Object,
                _pdfGenerator.Object,
                _blobStorageService.Object,
                Options.Create(new BlobStorageOptions { DefaultContainerName = ContainerName }),
                _mapper.Object,
                _logger.Object);
        }

        /// <summary>
        /// Compone il percorso felice di ProcessExportAsync: nessun sintomo, PDF generato
        /// e upload riusciti. Rimane al test da pilotare solo cio' che gli interessa
        /// (captor, stato iniziale, eccezioni). Usata solo da ProcessTests.
        /// </summary>
        protected ExportService SetupProcessing(ExportRequest request)
        {
            SetupGetByIdAsync(request);
            SetupSymptomFindAsync(NoSymptoms);
            SetupSymptomListMapping(Array.Empty<SymptomModel>());
            SetupPdfSuccess();
            SetupBlobUploadSuccess();
            return CreateService();
        }

        // --- Setup: repository ---

        protected void SetupGetByIdAsync(ExportRequest? result)
        {
            _capturedGetByIdToken = null;
            _exportRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, CancellationToken>((_, ct) => _capturedGetByIdToken = ct)
                .ReturnsAsync(result);
        }

        protected void SetupExportFindAsync(IReadOnlyList<ExportRequest> result)
        {
            _capturedExportPredicate = null;
            _exportRepository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<ExportRequest, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<ExportRequest, bool>>, CancellationToken>((expr, _) =>
                    _capturedExportPredicate = expr)
                .ReturnsAsync(result);
        }

        protected void SetupSymptomFindAsync(IReadOnlyList<Symptom> result)
        {
            _capturedSymptomPredicate = null;
            _capturedSymptomToken = null;
            _symptomRepository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<Symptom, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<Symptom, bool>>, CancellationToken>((expr, ct) =>
                {
                    _capturedSymptomPredicate = expr;
                    _capturedSymptomToken = ct;
                })
                .ReturnsAsync(result);
        }

        // --- Setup: collaboratori ---

        protected void SetupSymptomListMapping(IReadOnlyList<SymptomModel> models)
        {
            _mappedSymptomSource = null;
            _mapper
                .Setup(m => m.Map<IReadOnlyList<SymptomModel>>(It.IsAny<object>()))
                .Callback<object>(source => _mappedSymptomSource = (IReadOnlyList<Symptom>)source)
                .Returns(models);
        }

        protected void SetupPdfSuccess(byte[]? bytes = null) =>
            _pdfGenerator
                .Setup(g => g.GenerateSymptomReport(
                    It.IsAny<IReadOnlyList<SymptomModel>>(), It.IsAny<DateTime>()))
                .Returns(bytes ?? PdfBytes);

        protected void SetupBlobUploadSuccess()
        {
            _uploadedContainer = null;
            _uploadedBlobName = null;
            _uploadedContentType = null;
            _uploadedLength = 0;

            _blobStorageService
                .Setup(b => b.UploadAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Stream, string, CancellationToken>(
                    (container, blobName, content, contentType, _) =>
                    {
                        _uploadedContainer = container;
                        _uploadedBlobName = blobName;
                        _uploadedContentType = contentType;
                        // Lo stream viene chiuso dalla using solo al ritorno dal service:
                        // qui e' ancora aperto e contiene i byte del PDF.
                        _uploadedLength = content.Length;
                    })
                .ReturnsAsync("https://healthtrace.blob.core.windows.net/exports/report.pdf");
        }

        // Usata solo da QueryTests, per gli scenari di download.
        protected void SetupBlobDownload(Stream content)
        {
            _downloadedContainer = null;
            _downloadedBlobName = null;
            _downloadedToken = null;

            _blobStorageService
                .Setup(b => b.DownloadAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((container, blobName, ct) =>
                {
                    _downloadedContainer = container;
                    _downloadedBlobName = blobName;
                    _downloadedToken = ct;
                })
                .ReturnsAsync(content);
        }

        // --- Verify: logging ---

        protected void VerifyErrorLogged(Exception expected) =>
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((_, _) => true),
                    expected,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

        protected void VerifyNoErrorLogged() =>
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((_, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
    }
}