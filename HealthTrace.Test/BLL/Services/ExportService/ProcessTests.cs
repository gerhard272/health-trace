using System.Net;
using HealthTrace.BLL.Models;
using HealthTrace.DAL.Entities;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Test di ProcessExportAsync, il worker che trasforma una richiesta Pending in
    /// un file su blob. Il metodo ha una guardia di idempotenza in ingresso, un blocco
    /// di lavoro dentro un try/catch e due salvataggi: uno per il blocco in corso, uno
    /// per lo stato finale. I test coprono i tre rami della guardia, la selezione dei
    /// sintomi per id o per intervallo di date, e i due esiti possibili del try.
    /// Il collaboratore piu' importante da coprire e' il filtro del catch, che lascia
    /// passare OperationCanceledException senza trattarla come un fallimento.
    /// </summary>
    public class ExportServiceProcessTests : TestBase
    {
        // --- Idempotenza ---

        [Fact]
        public async Task ProcessExportAsync_UnknownRequest_ReturnsWithoutProcessing()
        {
            SetupGetByIdAsync(null);
            var service = CreateService();

            await service.ProcessExportAsync(ExportRequestId);

            // Un id inesistente non e' un errore da propagare: una coda at-least-once
            // puo' consegnare due volte lo stesso messaggio, e il secondo giro deve
            // uscire in silenzio invece di accodare un export vuoto.
            _exportRepository.Verify(r => r.Update(It.IsAny<ExportRequest>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _pdfGenerator.Verify(g => g.GenerateSymptomReport(
                It.IsAny<IReadOnlyList<SymptomModel>>(), It.IsAny<DateTime>()), Times.Never);
            _blobStorageService.Verify(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoErrorLogged();
        }

        [Theory]
        [InlineData(ExportStatus.Completed)]
        [InlineData(ExportStatus.Processing)]
        public async Task ProcessExportAsync_AlreadyCompletedOrProcessing_ReturnsWithoutProcessing(
            ExportStatus status)
        {
            SetupGetByIdAsync(ValidEntity(status: status));
            var service = CreateService();

            await service.ProcessExportAsync(ExportRequestId);

            // Solo questi due stati fermano il worker: Finished e Failed non passano
            // di qui, li coprono i test alle righe sotto.
            _exportRepository.Verify(r => r.Update(It.IsAny<ExportRequest>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _blobStorageService.Verify(b => b.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task ProcessExportAsync_FailedRequest_IsProcessedAgain()
        {
            // Il fallimento non e' uno stato definitivo: senza questo test, allungare
            // la guardia a Failed renderebbe ogni errore irreversibile.
            var entity = ValidEntity(status: ExportStatus.Failed);
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Completed, entity.Status);
            Assert.Null(entity.ErrorMessage);
        }

        // --- Selezione dei sintomi ---

        [Fact]
        public async Task ProcessExportAsync_WithSymptomIds_QueriesByOwnerAndRequestedIds()
        {
            var service = SetupProcessing(ValidEntity(symptomIds: new List<int> { 3, 7 }));
            SetupSymptomFindAsync(new[] { ValidSymptom(3), ValidSymptom(7) });

            await service.ProcessExportAsync(ExportRequestId);

            // Il predicato viene compilato e valutato davvero: gli id richiesti passano,
            // quelli estranei no. Nessun filtro per data quando gli id sono presenti.
            Assert.NotNull(_capturedSymptomPredicate);
            var predicate = _capturedSymptomPredicate!.Compile();
            Assert.True(predicate(ValidSymptom(3)));
            Assert.True(predicate(ValidSymptom(7)));
            Assert.False(predicate(ValidSymptom(5)));
            Assert.False(predicate(ValidSymptom(3, userId: OtherUserId)));
        }

        [Fact]
        public async Task ProcessExportAsync_WithoutSymptomIds_QueriesByDateRangeIncludingToDate()
        {
            var entity = ValidEntity(
                fromDate: new DateTime(2026, 9, 10),
                toDate: new DateTime(2026, 9, 12));
            var service = SetupProcessing(entity);
            SetupSymptomFindAsync(NoSymptoms);

            await service.ProcessExportAsync(ExportRequestId);

            // Il caso piu' delicato del service: il limite superiore e' spostato a
            // ToDate.Date.AddDays(1) e confrontato con <, cosi' l'ultimo giorno e'
            // incluso per tutto. Con <= sul solo ToDate le 23:59:59 andrebbero perse.
            Assert.NotNull(_capturedSymptomPredicate);
            var predicate = _capturedSymptomPredicate!.Compile();

            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 10, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 9, 23, 59, 59))));
            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 12, 23, 59, 59))));
            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 13, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, userId: OtherUserId)));
        }

        [Fact]
        public async Task ProcessExportAsync_Symptoms_AreOrderedByEventDateBeforeMapping()
        {
            var early = ValidSymptom(1, eventDate: new DateTime(2026, 1, 1));
            var late = ValidSymptom(2, eventDate: new DateTime(2026, 6, 1));
            var service = SetupProcessing(ValidEntity());
            // Il repository restituisce disordinati, come fa il database senza ORDER BY.
            SetupSymptomFindAsync(new[] { late, early });

            await service.ProcessExportAsync(ExportRequestId);

            // L'ordinamento avviene nel service, non nella query: e' l'unico posto
            // dove il report puo' presentare i sintomi in ordine cronologico.
            Assert.NotNull(_mappedSymptomSource);
            Assert.Equal(
                new List<int> { early.Id, late.Id },
                _mappedSymptomSource!.Select(s => s.Id).ToList());
        }

        // --- Percorso riuscito ---

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_MarksRequestAsCompletedAndClearsError()
        {
            var entity = ValidEntity(
                status: ExportStatus.Pending,
                errorMessage: "errore del tentativo precedente");
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Completed, entity.Status);
            // Il giro riuscito azzera il messaggio del fallimento: senza, l'utente
            // vedrebbe l'export completato con un errore addosso che non si e' piu'.
            Assert.Null(entity.ErrorMessage);
            Assert.NotNull(entity.BlobName);
        }

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_UploadsPdfUnderUserScopedBlobName()
        {
            var entity = ValidEntity(userId: OtherUserId);
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ContainerName, _uploadedContainer);
            Assert.Equal(PdfContentType, _uploadedContentType);
            Assert.Equal((long)PdfBytes.Length, _uploadedLength);
            // Il blob sta sotto il prefisso dell'utente: e' il nome che verra' usato
            // al download, e i due utenti non devono poter arrivare allo stesso file.
            Assert.NotNull(_uploadedBlobName);
            Assert.StartsWith($"{OtherUserId}/", _uploadedBlobName);
            Assert.EndsWith(".pdf", _uploadedBlobName);
            Assert.Equal(_uploadedBlobName, entity.BlobName);
        }

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_SetsTimestampedFileName()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            // Il timestamp viene da DateTime.UtcNow, che non e' iniettabile: qui si
            // verifica il formato, non il valore esatto.
            Assert.Matches(@"^sintomi-\d{8}-\d{6}\.pdf$", Assert.IsType<string>(entity.FileName));
        }

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_GeneratesPdfFromMappedModels()
        {
            var models = new[] { new SymptomModel { Id = 1 }, new SymptomModel { Id = 2 } };
            var service = SetupProcessing(ValidEntity());
            SetupSymptomListMapping(models);

            DateTime? generatedAt = null;
            _pdfGenerator
                .Setup(g => g.GenerateSymptomReport(
                    It.IsAny<IReadOnlyList<SymptomModel>>(), It.IsAny<DateTime>()))
                .Callback<IReadOnlyList<SymptomModel>, DateTime>((_, at) => generatedAt = at)
                .Returns(PdfBytes);

            var before = DateTime.UtcNow.AddSeconds(-5);

            await service.ProcessExportAsync(ExportRequestId);

            // Il generatore riceve i modelli e non le entita': e' l'unico punto in cui
            // il mapper partecipa al flusso, e la data e' UTC per il nome del file.
            _pdfGenerator.Verify(g => g.GenerateSymptomReport(
                It.Is<IReadOnlyList<SymptomModel>>(m => m.SequenceEqual(models)),
                It.IsAny<DateTime>()), Times.Once);
            Assert.NotNull(generatedAt);
            Assert.InRange(generatedAt!.Value, before, DateTime.UtcNow.AddSeconds(5));
        }

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_PersistsFinalStateAfterTheWork()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            // Due salvataggi: il blocco Processing e poi lo stato finale. Se il
            // secondo mancasse, la richiesta resterebbe Processing per sempre e la
            // guardia di idempotenza la considererebbe gia' in corso, senza ritentarla.
            _exportRepository.Verify(r => r.Update(entity), Times.Exactly(2));
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        // --- Percorso fallito ---

        [Fact]
        public async Task ProcessExportAsync_PdfGenerationThrows_MarksRequestAsFailed()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);
            var error = new InvalidOperationException("template del report mancante");
            _pdfGenerator
                .Setup(g => g.GenerateSymptomReport(
                    It.IsAny<IReadOnlyList<SymptomModel>>(), It.IsAny<DateTime>()))
                .Throws(error);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Failed, entity.Status);
            Assert.Equal("template del report mancante", entity.ErrorMessage);
            // Nessuna traccia di un file mai esistito: il fallimento e' avvenuto prima.
            Assert.Null(entity.BlobName);
            Assert.Null(entity.FileName);
            VerifyErrorLogged(error);
        }

        [Fact]
        public async Task ProcessExportAsync_BlobUploadThrows_MarksRequestAsFailed()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);
            var error = new HttpRequestException("il container non risponde");
            _blobStorageService
                .Setup(b => b.UploadAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Failed, entity.Status);
            Assert.Equal("il container non risponde", entity.ErrorMessage);
            VerifyErrorLogged(error);
        }

        [Fact]
        public async Task ProcessExportAsync_ErrorMessageLongerThanLimit_IsTruncated()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);
            var error = new InvalidOperationException(new string('x', MaxErrorLength + 500));
            _pdfGenerator
                .Setup(g => g.GenerateSymptomReport(
                    It.IsAny<IReadOnlyList<SymptomModel>>(), It.IsAny<DateTime>()))
                .Throws(error);

            await service.ProcessExportAsync(ExportRequestId);

            // ErrorMessage e' una colonna: senza taglio, un messaggio enorme da un
            // servizio esterno farebbe fallire il salvataggio della richiesta Failed,
            // perdendo anche l'informazione sul fallimento.
            Assert.Equal(MaxErrorLength, Assert.IsType<string>(entity.ErrorMessage).Length);
            Assert.Equal(new string('x', MaxErrorLength), entity.ErrorMessage);
        }

        [Fact]
        public async Task ProcessExportAsync_OperationCanceled_PropagatesAndLeavesRequestProcessing()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);
            _blobStorageService
                .Setup(b => b.UploadAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException());

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => service.ProcessExportAsync(ExportRequestId));

            // Il filtro "when (ex is not OperationCanceledException)" e' deliberato:
            // annullare non e' fallire, quindi l'eccezione risale al worker e la
            // richiesta resta in Processing, pronta per un giro successivo.
            Assert.Equal(ExportStatus.Processing, entity.Status);
            Assert.Null(entity.ErrorMessage);
            VerifyNoErrorLogged();
        }

        // --- Propagazione del token ---

        [Fact]
        public async Task ProcessExportAsync_ValidRequest_PropagatesCancellationToken()
        {
            var service = SetupProcessing(ValidEntity());
            using var cts = new CancellationTokenSource();

            await service.ProcessExportAsync(ExportRequestId, cts.Token);

            // Il token raggiunge tutte le chiamate: l'annullamento dell'host deve
            // interrompere il lavoro prima che venga scritto uno stato obsoleto.
            Assert.Equal(cts.Token, _capturedGetByIdToken);
            Assert.Equal(cts.Token, _capturedSymptomToken);
            _blobStorageService.Verify(b => b.UploadAsync(
                ContainerName, It.IsAny<string>(), It.IsAny<Stream>(),
                PdfContentType, cts.Token), Times.Once);
        }
    }
}
