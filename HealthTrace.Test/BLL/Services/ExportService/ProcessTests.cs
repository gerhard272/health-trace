using System.Net;
using HealthTrace.BLL.Models;
using HealthTrace.DAL.Entities;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Tests for ProcessExportAsync, the worker that turns a Pending request into
    /// a file in blob storage. The method has an idempotency guard on entry, a block
    /// of work inside a try/catch and two saves: one for the in-progress lock, one
    /// for the final state. The tests cover the three branches of the guard, symptom
    /// selection by id or by date range, and the two possible outcomes of the try.
    /// The most important piece to cover is the catch filter, which lets
    /// OperationCanceledException through without treating it as a failure.
    /// </summary>
    public class ExportServiceProcessTests : TestBase
    {
        // --- Idempotency ---

        [Fact]
        public async Task ProcessExportAsync_UnknownRequest_ReturnsWithoutProcessing()
        {
            SetupGetByIdAsync(null);
            var service = CreateService();

            await service.ProcessExportAsync(ExportRequestId);

            // A non-existent id is not an error to propagate: an at-least-once queue
            // may deliver the same message twice, and the second run must
            // exit silently instead of queuing an empty export.
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

            // Only these two states stop the worker: Pending and Failed do not go
            // through here, they are covered by the tests below.
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
            // Failure is not a final state: without this test, extending
            // the guard to Failed would make every error irreversible.
            var entity = ValidEntity(status: ExportStatus.Failed);
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Completed, entity.Status);
            Assert.Null(entity.ErrorMessage);
        }

        // --- Symptom selection ---

        [Fact]
        public async Task ProcessExportAsync_WithSymptomIds_QueriesByOwnerAndRequestedIds()
        {
            var service = SetupProcessing(ValidEntity(symptomIds: new List<int> { 3, 7 }));
            SetupSymptomFindAsync(new[] { ValidSymptom(3), ValidSymptom(7) });

            await service.ProcessExportAsync(ExportRequestId);

            // The predicate is actually compiled and evaluated: the requested ids pass,
            // the others do not. No date filter when ids are present.
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

            // The trickiest case in the service: the upper bound is moved to
            // ToDate.Date.AddDays(1) and compared with <, so the last day is
            // included entirely. With <= on ToDate alone, 23:59:59 would be lost.
            Assert.NotNull(_capturedSymptomPredicate);
            var predicate = _capturedSymptomPredicate!.Compile();

            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 10, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 9, 23, 59, 59))));
            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 12, 23, 59, 59))));
            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 13, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, userId: OtherUserId)));
        }

        [Fact]
        public async Task ProcessExportAsync_WithOnlyFromDate_AppliesLowerBoundAndNoUpperBound()
        {
            // ToDate null: the service must build toExclusive = null and let the
            // "toExclusive == null" short-circuit cut out the second filter.
            var entity = ValidEntity(fromDate: new DateTime(2026, 9, 10), toDate: null);
            var service = SetupProcessing(entity);
            SetupSymptomFindAsync(NoSymptoms);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.NotNull(_capturedSymptomPredicate);
            var predicate = _capturedSymptomPredicate!.Compile();

            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 9, 23, 59, 59))));
            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 10, 0, 0, 0))));
            // The assertion that closes the gap: without an upper bound, a symptom
            // three years later must be included. If the short-circuit were inverted, this
            // would return false and the export "from 10/09" would lose everything after.
            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2029, 12, 31, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, userId: OtherUserId)));
        }

        [Fact]
        public async Task ProcessExportAsync_WithOnlyToDate_AppliesUpperBoundAndNoLowerBound()
        {
            // Mirror of the previous case: FromDate null gives from = null, and the
            // "from == null" short-circuit must skip the lower-bound filter.
            var entity = ValidEntity(fromDate: null, toDate: new DateTime(2026, 9, 12));
            var service = SetupProcessing(entity);
            SetupSymptomFindAsync(NoSymptoms);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.NotNull(_capturedSymptomPredicate);
            var predicate = _capturedSymptomPredicate!.Compile();

            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 12, 23, 59, 59))));
            Assert.False(predicate(ValidSymptom(1, eventDate: new DateTime(2026, 9, 13, 0, 0, 0))));
            // As above: without a lower bound the whole past must be included, otherwise
            // the export "until 12/09" would lose the earlier medical history.
            Assert.True(predicate(ValidSymptom(1, eventDate: new DateTime(2000, 1, 1, 0, 0, 0))));
            Assert.False(predicate(ValidSymptom(1, userId: OtherUserId)));
        }

        [Fact]
        public async Task ProcessExportAsync_Symptoms_AreOrderedByEventDateBeforeMapping()
        {
            var early = ValidSymptom(1, eventDate: new DateTime(2026, 1, 1));
            var late = ValidSymptom(2, eventDate: new DateTime(2026, 6, 1));
            var service = SetupProcessing(ValidEntity());
            // The repository returns them unordered, as the database does without ORDER BY.
            SetupSymptomFindAsync(new[] { late, early });

            await service.ProcessExportAsync(ExportRequestId);

            // Sorting happens in the service, not in the query: it is the only place
            // where the report can present symptoms in chronological order.
            Assert.NotNull(_mappedSymptomSource);
            Assert.Equal(
                new List<int> { early.Id, late.Id },
                _mappedSymptomSource!.Select(s => s.Id).ToList());
        }

        // --- Success path ---

        [Fact]
        public async Task ProcessExportAsync_SuccessfulRun_MarksRequestAsCompletedAndClearsError()
        {
            var entity = ValidEntity(
                status: ExportStatus.Pending,
                errorMessage: "error from the previous attempt");
            var service = SetupProcessing(entity);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Completed, entity.Status);
            // A successful run clears the failure message: otherwise the user
            // would see the completed export with an error that no longer applies.
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
            // The blob lives under the user's prefix: it is the name used
            // for the download, and two users must never reach the same file.
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

            // The timestamp comes from DateTime.UtcNow, which cannot be injected: here we
            // check the format, not the exact value.
            Assert.Matches(@"^symptoms-\d{8}-\d{6}\.pdf$", Assert.IsType<string>(entity.FileName));
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

            // The generator receives models and not entities: it is the only place where
            // the mapper takes part in the flow, and the date is UTC for the file name.
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

            // Two saves: the Processing lock and then the final state. If the
            // second were missing, the request would stay Processing forever and the
            // idempotency guard would consider it in progress, never retrying it.
            _exportRepository.Verify(r => r.Update(entity), Times.Exactly(2));
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        // --- Failure path ---

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
            // No trace of a file that never existed: the failure happened earlier.
            Assert.Null(entity.BlobName);
            Assert.Null(entity.FileName);
            VerifyErrorLogged(error);
        }

        [Fact]
        public async Task ProcessExportAsync_BlobUploadThrows_MarksRequestAsFailed()
        {
            var entity = ValidEntity();
            var service = SetupProcessing(entity);
            var error = new HttpRequestException("the container is not responding");
            _blobStorageService
                .Setup(b => b.UploadAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);

            await service.ProcessExportAsync(ExportRequestId);

            Assert.Equal(ExportStatus.Failed, entity.Status);
            Assert.Equal("the container is not responding", entity.ErrorMessage);
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

            // ErrorMessage is a column: without truncation, a huge message from an
            // external service would make saving the Failed request fail,
            // losing the failure information as well.
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

            // The "when (ex is not OperationCanceledException)" filter is deliberate:
            // cancelling is not failing, so the exception bubbles up to the worker and the
            // request stays Processing, ready for a later run.
            Assert.Equal(ExportStatus.Processing, entity.Status);
            Assert.Null(entity.ErrorMessage);
            VerifyNoErrorLogged();
        }

        // --- Token propagation ---

        [Fact]
        public async Task ProcessExportAsync_ValidRequest_PropagatesCancellationToken()
        {
            var service = SetupProcessing(ValidEntity());
            using var cts = new CancellationTokenSource();

            // Both SaveChangesAsync calls receive the token along with everything else:
            // without it, cancellation would reach the collaborators but the write
            // would start anyway, and a stale state would reach the database.
            // The setup goes AFTER SetupProcessing because that calls CreateService,
            // which already registers SaveChangesAsync and would be overwritten here.
            var saveTokens = new List<CancellationToken>();
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveTokens.Add(ct))
                .ReturnsAsync(1);

            await service.ProcessExportAsync(ExportRequestId, cts.Token);

            Assert.Equal(cts.Token, _capturedGetByIdToken);
            Assert.Equal(cts.Token, _capturedSymptomToken);
            Assert.Equal(new[] { cts.Token, cts.Token }, saveTokens);
            _blobStorageService.Verify(b => b.UploadAsync(
                ContainerName, It.IsAny<string>(), It.IsAny<Stream>(),
                PdfContentType, cts.Token), Times.Once);
        }
    }
}
