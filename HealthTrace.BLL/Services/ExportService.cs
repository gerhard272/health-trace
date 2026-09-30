using AutoMapper;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.DAL.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthTrace.BLL.Services
{
    public class ExportService : IExportService
    {
        private const string PdfContentType = "application/pdf";
        private const int MaxErrorLength = 1000;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<ExportRequest> _exportRepository;
        private readonly IGenericRepository<Symptom> _symptomRepository;
        private readonly IPdfGenerator _pdfGenerator;
        private readonly IBlobStorageService _blobStorageService;
        private readonly BlobStorageOptions _blobOptions;
        private readonly IMapper _mapper;
        private readonly ILogger<ExportService> _logger;

        public ExportService(
            IUnitOfWork unitOfWork,
            IPdfGenerator pdfGenerator,
            IBlobStorageService blobStorageService,
            IOptions<BlobStorageOptions> blobOptions,
            IMapper mapper,
            ILogger<ExportService> logger)
        {
            _unitOfWork = unitOfWork;
            _exportRepository = unitOfWork.Repository<ExportRequest>();
            _symptomRepository = unitOfWork.Repository<Symptom>();
            _pdfGenerator = pdfGenerator;
            _blobStorageService = blobStorageService;
            _blobOptions = blobOptions.Value;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ExportRequestModel> RequestExportAsync(int userId, 
            ExportRequestCreateModel model, 
            CancellationToken cancellationToken = default)
        {
            var entity = new ExportRequest
            {
                UserId = userId,
                Status = ExportStatus.Pending,
                SymptomIds = model.SymptomIds?.Distinct().ToList() ?? new List<int>(),
                FromDate = model.FromDate,
                ToDate = model.ToDate
            };

            await _exportRepository.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToModel(entity);
        }

        public async Task ProcessExportAsync(int exportRequestId, 
            CancellationToken cancellationToken = default)
        {
            var request = await _exportRepository.GetByIdAsync(exportRequestId, cancellationToken);

            // Idempotenza: le code (at-least-once) possono consegnare lo stesso messaggio due volte
            // questo lo controlla e se è già in lavorazione si ferma
            if (request == null || 
                request.Status is ExportStatus.Completed or ExportStatus.Processing)
                return;

            request.Status = ExportStatus.Processing;
            _exportRepository.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var symptoms = await LoadSymptomsAsync(request, cancellationToken);
                var symptomModels = _mapper.Map<IReadOnlyList<SymptomModel>>(symptoms);

                var now = DateTime.UtcNow;
                var pdfBytes = _pdfGenerator.GenerateSymptomReport(symptomModels, now);

                var blobName = $"{request.UserId}/{Guid.NewGuid()}.pdf";
                using var stream = new MemoryStream(pdfBytes);
                await _blobStorageService.UploadAsync(
                    _blobOptions.DefaultContainerName, 
                    blobName, 
                    stream, 
                    PdfContentType, 
                    cancellationToken);

                request.BlobName = blobName;
                request.FileName = $"sintomi-{now:yyyyMMdd-HHmmss}.pdf";
                request.Status = ExportStatus.Completed;
                request.ErrorMessage = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Export {ExportRequestId} fallito", exportRequestId);
                request.Status = ExportStatus.Failed;
                request.ErrorMessage = Truncate(ex.Message, MaxErrorLength);
            }

            _exportRepository.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ExportRequestModel>> GetHistoryAsync(int userId, 
            CancellationToken cancellationToken = default)
        {
            var entities = await _exportRepository
                .FindAsync(e => e.UserId == userId, cancellationToken);

            return entities
                .OrderByDescending(e => e.CreatedAt)
                .Select(ToModel)
                .ToList();
        }

        public async Task<ExportRequestModel?> GetByIdAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, exportRequestId, cancellationToken);
            return entity == null ? null : ToModel(entity);
        }

        public async Task<ExportFileModel?> GetFileAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken = default)
        {
            var entity = await GetOwnedEntityAsync(userId, exportRequestId, cancellationToken);

            if (entity == null || entity.Status != ExportStatus.Completed || entity.BlobName == null)
                return null;

            var content = await _blobStorageService.DownloadAsync(
                _blobOptions.DefaultContainerName, entity.BlobName, cancellationToken);

            return new ExportFileModel
            {
                Content = content,
                FileName = entity.FileName ?? "sintomi.pdf",
                ContentType = PdfContentType
            };
        }

        private async Task<IReadOnlyList<Symptom>> LoadSymptomsAsync(ExportRequest request, 
            CancellationToken cancellationToken)
        {
            var userId = request.UserId;
            IReadOnlyList<Symptom> symptoms;

            if (request.SymptomIds.Count > 0)
            {
                var ids = request.SymptomIds;
                symptoms = await _symptomRepository.FindAsync(
                    s => s.UserId == userId && ids.Contains(s.Id),
                    cancellationToken);
            }
            else
            {
                var from = request.FromDate?.Date;
                var toExclusive = request.ToDate?.Date.AddDays(1);

                symptoms = await _symptomRepository.FindAsync(
                    s => s.UserId == userId
                         && (from == null || s.EventDate >= from)
                         && (toExclusive == null || s.EventDate < toExclusive),
                    cancellationToken);
            }

            return symptoms.OrderBy(s => s.EventDate).ToList();
        }

        private async Task<ExportRequest?> GetOwnedEntityAsync(int userId, 
            int exportRequestId, 
            CancellationToken cancellationToken)
        {
            var results = await _exportRepository.FindAsync(
                e => e.Id == exportRequestId && e.UserId == userId,
                cancellationToken);

            return results.FirstOrDefault();
        }

        private static ExportRequestModel ToModel(ExportRequest entity) => new()
        {
            Id = entity.Id,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            SymptomIds = entity.SymptomIds,
            FromDate = entity.FromDate,
            ToDate = entity.ToDate,
            FileName = entity.FileName,
            ErrorMessage = entity.ErrorMessage
        };

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}