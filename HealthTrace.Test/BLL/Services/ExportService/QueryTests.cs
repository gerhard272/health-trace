using System.Linq.Expressions;
using HealthTrace.DAL.Entities;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportService
{
    /// <summary>
    /// Tests for the read methods: GetHistoryAsync, GetByIdAsync and GetFileAsync.
    /// All three share the authorization boundary, which is the delicate part here:
    /// GetOwnedEntityAsync filters on Id and UserId together, and the mocks always return
    /// whatever they are asked for. So the predicates are actually compiled and evaluated
    /// instead of trusting the return value: if the owner clause were dropped,
    /// a test based only on the result would still pass.
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

            // The history may come back unordered from the database, so the service
            // sorts it: newest to oldest, by date only.
            Assert.Equal(new List<int> { newer.Id, older.Id }, result.Select(r => r.Id).ToList());

            Assert.NotNull(_capturedExportPredicate);
            var predicate = _capturedExportPredicate!.Compile();
            Assert.True(predicate(newer));
            // Owner only: another user's history must not be mixed in.
            Assert.False(predicate(ValidEntity(id: 12, userId: OtherUserId)));
        }

        [Fact]
        public async Task GetHistoryAsync_MixedStatuses_MapsFileNameAndErrorMessage()
        {
            var done = ValidEntity(
                id: 10, status: ExportStatus.Completed, fileName: "symptoms-20260101-101010.pdf");
            var broken = ValidEntity(id: 11, status: ExportStatus.Failed, errorMessage: "blob irraggiungibile");
            SetupExportFindAsync(new[] { done, broken });
            var service = CreateService();

            var result = await service.GetHistoryAsync(UserId);

            // Each state exposes its own fields: a completed export shows the file,
            // a failed one the reason. They are different fields, not a single fallback.
            Assert.Equal(ExportStatus.Completed, result[0].Status);
            Assert.Equal("symptoms-20260101-101010.pdf", result[0].FileName);
            Assert.Null(result[0].ErrorMessage);
            Assert.Equal(ExportStatus.Failed, result[1].Status);
            Assert.Equal("blob irraggiungibile", result[1].ErrorMessage);
            Assert.Null(result[1].FileName);
        }

        [Fact]
        public async Task GetHistoryAsync_NoExports_ReturnsEmptyList()
        {
            // A user who never requested an export: the normal case for a new
            // account, not an error. The list must reach the controller empty, so it
            // can turn it into a 200 with an empty list and not a 404.
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

            // A long history must not ignore the cancellation of the HTTP request
            // that triggered it, otherwise the query keeps running.
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

            // The predicate must require Id and UserId together: it is the ownership
            // check that prevents reading another user's export.
            Assert.NotNull(_capturedExportPredicate);
            var predicate = _capturedExportPredicate!.Compile();
            Assert.True(predicate(entity));
            Assert.False(predicate(ValidEntity(userId: OtherUserId)));
            Assert.False(predicate(ValidEntity(id: ExportRequestId + 1)));
        }

        [Fact]
        public async Task GetByIdAsync_UnknownRequest_ReturnsNull()
        {
            // No match: the service returns null instead of throwing,
            // so the controller can turn it into a 404 without special cases.
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
                fileName: "symptoms-20260101-101010.pdf");
            SetupExportFindAsync(new[] { entity });
            SetupBlobDownload(content);
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            Assert.NotNull(result);
            Assert.Same(content, result!.Content);
            Assert.Equal("symptoms-20260101-101010.pdf", result.FileName);
            Assert.Equal(PdfContentType, result.ContentType);

            // The container comes from the options and the blob from the record: neither
            // comes from the client, so there is no way to read another path.
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

            // Covers both the non-completed export and the completed one without a blob:
            // in both cases we exit before touching storage. A useless download
            // would cost traffic and, on Completed without a blob, would fail
            // with an error instead of returning a clean null.
            Assert.Null(result);
            _blobStorageService.Verify(b => b.DownloadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            VerifyNoErrorLogged();
        }

        [Fact]
        public async Task GetFileAsync_UnknownRequest_ReturnsNullWithoutDownloading()
        {
            // No matching request: same silent exit as the previous
            // cases, without even evaluating the entity fields.
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
            // A completed export without a file name would hand the client an
            // empty string, which would be shown as an unnamed attachment.
            var entity = ValidEntity(
                status: ExportStatus.Completed, blobName: "1/abc.pdf", fileName: null);
            SetupExportFindAsync(new[] { entity });
            SetupBlobDownload(new MemoryStream());
            var service = CreateService();

            var result = await service.GetFileAsync(UserId, ExportRequestId);

            Assert.NotNull(result);
            Assert.Equal("symptoms.pdf", result!.FileName);
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

            // The token reaches both the lookup and the download: without it, a large
            // file would keep downloading after the request was cancelled.
            Assert.Equal(cts.Token, _downloadedToken);
            _exportRepository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<ExportRequest, bool>>>(), cts.Token), Times.Once);
        }
    }
}
