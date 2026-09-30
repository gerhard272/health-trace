using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Tests for RequestExportAsync. The method does not read from the database: it builds
    /// the entity from scratch and queues it, so the tests look at the data passed to
    /// AddAsync, its order relative to SaveChangesAsync and the returned model.
    /// The class never names the ExportService type: it only uses CreateService(), so
    /// it does not need the alias declared in TestBase.cs.
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

            // The coalesce is needed because Distinct() on a null list throws:
            // without it, the entity would have null SymptomIds and LoadSymptomsAsync would read
            // .Count on a null reference when the queue processes it.
            Assert.NotNull(_addedEntity);
            Assert.Empty(_addedEntity!.SymptomIds);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_PersistsRequestForCallerAsPending()
        {
            var service = CreateService();

            await service.RequestExportAsync(
                OtherUserId, CreateModel(symptomIds: new List<int> { 1 }));

            // UserId comes from the argument and not from the model: not even a hand-built
            // model can queue an export for a different user.
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

            // Dates travel raw, without end-of-day normalization: that is
            // done by LoadSymptomsAsync with ToDate.Date.AddDays(1), not in this phase.
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

            // The setups go AFTER CreateService(), not before: the factory already registers
            // SaveChangesAsync, and a later setup overrides the earlier one,
            // dropping the callback. In this order the list is actually filled.
            _exportRepository
                .Setup(r => r.AddAsync(It.IsAny<ExportRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ExportRequest, CancellationToken>((_, _) => calls.Add("AddAsync"))
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ => calls.Add("SaveChangesAsync"))
                .ReturnsAsync(1);

            await service.RequestExportAsync(UserId, CreateModel());

            // Swapping the two steps would save a request that was never
            // queued: the entity would stay pending and the user would see nothing.
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

            // Both receive the same token: propagating it only to AddAsync
            // would leave the write running after a cancellation request.
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
            // FileName and ErrorMessage stay null until the queue runs: they are
            // set only by the Completed and Failed branches of ProcessExportAsync.
            Assert.Null(result.FileName);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task RequestExportAsync_ValidModel_DoesNotReadAnyExistingExport()
        {
            var service = CreateService();

            await service.RequestExportAsync(UserId, CreateModel(symptomIds: new List<int> { 1, 2 }));

            // Symptom deduplication happens only on the model: the service does not validate the ids
            // against the user's data, so queuing stays a plain write.
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