using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Test di RequestExportAsync. Il metodo non legge dal database: costruisce
    /// l'entita' da zero e la accoda, quindi i test guardano i dati passati ad
    /// AddAsync, il loro ordine rispetto a SaveChangesAsync e il modello restituito.
    /// La classe non nomina mai il tipo ExportService: usa solo CreateService(), quindi
    /// non ha bisogno dell'alias dichiarato in TestBase.cs.
    /// </summary>
    public class ExportServiceRequestTests : TestBase
    {
        [Fact]
        public async Task RequestExportAsync_SymptomIdsWithDuplicates_PersistsDistinctIdsInOriginalOrder()
        {
            var service = CreateService();

            await service.RequestExportAsync(
                UserId, CreateModel(symptomIds: new List<int> { 7, 3, 7, 9, 3 }));

            Assert.NotNull(_addedEntity);
            Assert.Equal(new List<int> { 7, 3, 9 }, _addedEntity!.SymptomIds);
        }

        [Fact]
        public async Task RequestExportAsync_NullSymptomIds_PersistsEmptyList()
        {
            var service = CreateService();

            await service.RequestExportAsync(UserId, CreateModel());

            // Il coalesce serve perche' Distinct() su una lista nulla va in errore:
            // senza, l'entita' avrebbe SymptomIds null e LoadSymptomsAsync leggerebbe
            // .Count su un riferimento nullo quando la coda la processa.
            Assert.NotNull(_addedEntity);
            Assert.Empty(_addedEntity!.SymptomIds);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_PersistsRequestForCallerAsPending()
        {
            var service = CreateService();

            await service.RequestExportAsync(
                OtherUserId, CreateModel(symptomIds: new List<int> { 1 }));

            // UserId arriva dall'argomento e non dal modello: nemmeno un model
            // costruito a mano puo' accodare un export per un utente diverso.
            Assert.NotNull(_addedEntity);
            Assert.Equal(OtherUserId, _addedEntity!.UserId);
            Assert.Equal(ExportStatus.Pending, _addedEntity.Status);
        }

        [Fact]
        public async Task RequestExportAsync_DateRange_CopiesFromAndToToEntityAndModel()
        {
            var from = new DateTime(2026, 9, 1);
            var to = new DateTime(2026, 9, 30);
            var service = CreateService();

            var result = await service.RequestExportAsync(UserId, CreateModel(fromDate: from, toDate: to));

            // Le date viaggiano grezze, senza normalizzazione a fine giornata: quello
            // lo fa LoadSymptomsAsync con ToDate.Date.AddDays(1), non questa fase.
            Assert.NotNull(_addedEntity);
            Assert.Equal(from, _addedEntity!.FromDate);
            Assert.Equal(to, _addedEntity.ToDate);
            Assert.Equal(from, result.FromDate);
            Assert.Equal(to, result.ToDate);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_AddsEntityBeforeSaving()
        {
            var calls = new List<string>();
            var service = CreateService();

            // I setup vanno DOPO CreateService(), non prima: il factory registra gia'
            // SaveChangesAsync, e un setup successivo sovrascrive quello precedente,
            // annullando la callback. In questo ordine la lista si riempie davvero.
            _exportRepository
                .Setup(r => r.AddAsync(It.IsAny<ExportRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRequest, CancellationToken>((_, _) => calls.Add("AddAsync"))
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ => calls.Add("SaveChangesAsync"))
                .ReturnsAsync(1);

            await service.RequestExportAsync(UserId, CreateModel());

            // Invertire i due passi salverebbe una richiesta che non e' mai stata
            // accodata: l'entita' resterebbe in attesa e l'utente non vedrebbe nulla.
            Assert.Equal(new[] { "AddAsync", "SaveChangesAsync" }, calls);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_PropagatesCancellationToken()
        {
            CancellationToken? addToken = null, saveToken = null;
            var service = CreateService();

            _exportRepository
                .Setup(r => r.AddAsync(It.IsAny<ExportRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRequest, CancellationToken>((_, ct) => addToken = ct)
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);

            using var cts = new CancellationTokenSource();

            await service.RequestExportAsync(UserId, CreateModel(), cts.Token);

            // Entrambe ricevono lo stesso token: propagarlo solo ad AddAsync
            // lascerebbe la scrittura attiva dopo una richiesta di annullamento.
            Assert.Equal(cts.Token, addToken);
            Assert.Equal(cts.Token, saveToken);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_ReturnsPendingRequestModel()
        {
            var service = CreateService();

            var result = await service.RequestExportAsync(
                UserId, CreateModel(symptomIds: new List<int> { 4, 5 }));

            Assert.Equal(ExportStatus.Pending, result.Status);
            Assert.Equal(new List<int> { 4, 5 }, result.SymptomIds);
            // FileName ed ErrorMessage restano null finche' la coda non gira: vengono
            // valorizzati solo dai rami Completed e Failed di ProcessExportAsync.
            Assert.Null(result.FileName);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_DoesNotReadAnyExistingExport()
        {
            var service = CreateService();

            await service.RequestExportAsync(UserId, CreateModel(symptomIds: new List<int> { 1, 2 }));

            // La deduplica dei sintomi e' solo sul model: il service non valida gli id
            // contro i dati dell'utente, quindi l'accodamento resta una scrittura secca.
            _exportRepository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<ExportRequest, bool>>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _exportRepository.Verify(r => r.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _exportRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}