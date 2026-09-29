using System.Linq.Expressions;
using HealthTrace.DAL.Entities;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Test dei metodi di lettura: GetHistoryAsync, GetByIdAsync e GetFileAsync.
    /// I tre condividono il confine di autorizzazione, che qui e' la parte delicata:
    /// GetOwnedEntityAsync filtra su Id e UserId insieme, e i mock restituiscono sempre
    /// quello che viene loro chiesto. Percio' i predicati vengono compilati e valutati
    /// davvero invece di fidarsi del valore di ritorno: se la clausola sul proprietario
    /// saltasse, un test basato solo sul risultato passerebbe comunque.
    /// </summary>
    public class ExportServiceQueryTests : TestBase
    {
        // --- GetHistoryAsync ---

        [Fact]
        public async Task GetHistoryAsync_AnyExports_FiltersByOwnerAndReturnsNewestFirst()
        {
            var older = ValidEntity(id: 10, createdAt: new DateTime(2026, 1, 1));
            var newer = ValidEntity(id: 11, createdAt: new DateTime(2026, 6, 1));
            SetupExportFindAsync(new[] { older, newer });
            var service = CreateService();

            var result = await service.GetHistoryAsync(UserId);

            // La cronologia puo' arrivare disordinata dal database, quindi il service
            // riordina: dalla piu' recente alla piu' vecchia, unico criterio la data.
            Assert.Equal(new List<int> { newer.Id, older.Id }, result.Select(r => r.Id).ToList());

            Assert.NotNull(_capturedExportPredicate);
            var predicate = _capturedExportPredicate!.Compile();
            Assert.True(predicate(newer));
            // Solo il proprietario: la cronologia di un altro utente non deve mescolarsi.
            Assert.False(predicate(ValidEntity(id: 12, userId: OtherUserId)));
        }

        [Fact]
        public async Task GetHistoryAsync_MixedStatuses_MapsFileNameAndErrorMessage()
        {
            var done = ValidEntity(
                id: 10, status: ExportStatus.Completed, fileName: "sintomi-20260101-101010.pdf");
            var broken = ValidEntity(id: 11, status: ExportStatus.Failed, errorMessage: "blob irraggiungibile");
            SetupExportFindAsync(new[] { done, broken });
            var service = CreateService();

            var result = await service.GetHistoryAsync(UserId);

            // Ogni stato espone i propri campi: un export completato mostra il file,
            // uno fallito il motivo. Sono campi diversi, non un fallback unico.
            Assert.Equal(ExportStatus.Completed, result[0].Status);
            Assert.Equal("sintomi-20260101-101010.pdf", result[0].FileName);
            Assert.Null(result[0].ErrorMessage);
            Assert.Equal(ExportStatus.Failed, result[1].Status);
            Assert.Equal("blob irraggiungibile", result[1].ErrorMessage);
            Assert.Null(result[1].FileName);
        }

        [Fact]
        public async Task GetHistoryAsync_NoExports_ReturnsEmptyList()
        {
            // Utente che non ha mai chiesto un export: il caso normale di un account
            // nuovo, non un errore. La lista deve arrivare vuota al controller, che la
            // deve poter tradurre in un 200 con elenco vuoto e non in un 404.
            SetupExportFindAsync(NoExports);
            var service = CreateService();

            var result = await service.GetHistoryAsync(UserId);

            Assert.NotNull(result);
            Assert.Empty(result);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task GetHistoryAsync_ValidUser_PropagatesCancellationToken()
        {
            SetupExportFindAsync(NoExports);
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            await service.GetHistoryAsync(UserId, cts.Token);

            // Una cronologia lunga non deve ignorare l'annullamento della richiesta
            // HTTP che l'ha innescata, altrimenti la query continua a girare.
            _exportRepository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<ExportRequest, bool>>>(), cts.Token), Times.Once);
        }

        // --- GetByIdAsync ---

        [Fact]
        public async Task GetByIdAsync_OwnedRequest_ReturnsRequestModel()
        {
            var entity = ValidEntity(status: ExportStatus.Completed, fileName: "report.pdf");
            SetupExportFindAsync(new[] { entity });
            var service = CreateService();

            var result = await service.GetByIdAsync(UserId, ExportRequestId);

            Assert.NotNull(result);
            Assert.Equal(ExportRequestId, result!.Id);
            Assert.Equal(ExportStatus.Completed, result.Status);

            // Il predicato deve richiedere Id e UserId insieme: e' il controllo di
            // proprieta' che impedisce di leggere l'export di un altro utente.
            Assert.NotNull(_capturedExportPredicate);
            var predicate = _capturedExportPredicate!.Compile();
            Assert.True(predicate(entity));
            Assert.False(predicate(ValidEntity(userId: OtherUserId)));
            Assert.False(predicate(ValidEntity(id: ExportRequestId + 1)));
        }

        [Fact]
        public async Task GetByIdAsync_UnknownRequest_ReturnsNull()
        {
            // Nessuna corrispondenza: il service restituisce null invece di lanciare,
            // cosi' il controller puo' tradurlo in 404 senza casi speciali.
            SetupExportFindAsync(NoExports);
            var service = CreateService();

            var result = await service.GetByIdAsync(UserId, ExportRequestId);

            Assert.Null(result);
            VerifyNoErrorLogged();
        }

        // --- GetFileAsync ---

        [Fact]
        public async Task GetFileAsync_CompletedRequest_DownloadsBlobFromConfiguredContainer()
        {
            var content = new MemoryStream(new byte[] { 9, 9, 9 });
            var entity = ValidEntity(
                status: ExportStatus.Completed,
                blobName: $"{UserId}/abc-123.pdf",
                fileName: "sintomi-20260101-101010.pdf");
            SetupExportFindAsync(new[] { entity });
            SetupBlobDownload(content);
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            Assert.NotNull(result);
            Assert.Same(content, result!.Content);
            Assert.Equal("sintomi-20260101-101010.pdf", result.FileName);
            Assert.Equal(PdfContentType, result.ContentType);

            // Il container viene dalle options e il blob dal record: nessuno dei due
            // arriva dal client, quindi non c'e' modo di leggere un altro percorso.
            Assert.Equal(ContainerName, _downloadedContainer);
            Assert.Equal(entity.BlobName, _downloadedBlobName);
        }

        [Theory]
        [InlineData(ExportStatus.Pending, "1/abc.pdf")]
        [InlineData(ExportStatus.Processing, "1/abc.pdf")]
        [InlineData(ExportStatus.Failed, "1/abc.pdf")]
        [InlineData(ExportStatus.Completed, null)]
        public async Task GetFileAsync_NotReadyOrWithoutBlob_ReturnsNullWithoutDownloading(
            ExportStatus status, string? blobName)
        {
            var entity = ValidEntity(status: status, blobName: blobName, fileName: "report.pdf");
            SetupExportFindAsync(new[] { entity });
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            // Copre sia l'export non completato sia quello completato senza blob:
            // in entrambi i casi si esce prima di toccare lo storage. Un download
            // inutilizzato costerebbe traffico e, su Completed senza blob, fallirebbe
            // con un errore invece di restituire un null pulito.
            Assert.Null(result);
            _blobStorageService.Verify(b => b.DownloadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task GetFileAsync_UnknownRequest_ReturnsNullWithoutDownloading()
        {
            // Nessuna richiesta corrispondente: stessa uscita silenziosa dei casi
            // precedenti, senza nemmeno arrivare a valutare i campi dell'entity.
            SetupExportFindAsync(NoExports);
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            Assert.Null(result);
            _blobStorageService.Verify(b => b.DownloadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetFileAsync_CompletedWithoutFileName_UsesFallbackName()
        {
            // Un export completato ma privo di nome file consegnerebbe al client una
            // stringa vuota, che verrebbe mostrata come allegato senza nome.
            var entity = ValidEntity(
                status: ExportStatus.Completed, blobName: "1/abc.pdf", fileName: null);
            SetupExportFindAsync(new[] { entity });
            SetupBlobDownload(new MemoryStream());
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            Assert.NotNull(result);
            Assert.Equal("sintomi.pdf", result!.FileName);
            Assert.Equal(PdfContentType, result.ContentType);
        }

        [Fact]
        public async Task GetFileAsync_ValidRequest_PropagatesCancellationToken()
        {
            var entity = ValidEntity(status: ExportStatus.Completed, blobName: "1/abc.pdf");
            SetupExportFindAsync(new[] { entity });
            SetupBlobDownload(new MemoryStream());
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            await service.GetFileAsync(UserId, ExportRequestId, cts.Token);

            // Il token arriva sia alla ricerca sia al download: senza quello, un file
            // grande continuerebbe a scaricarsi dopo l'annullamento della richiesta.
            Assert.Equal(cts.Token, _downloadedToken);
            _exportRepository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<ExportRequest, bool>>>(), cts.Token), Times.Once);
        }
    }
}
