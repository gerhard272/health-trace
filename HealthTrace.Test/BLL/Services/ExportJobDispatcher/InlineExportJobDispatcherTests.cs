using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportJobDispatcher
{
    /// <summary>
    /// Covers InlineExportJobDispatcher, the implementation that runs the export in the same
    /// HTTP request that asked for it. It has no logic of its own: it delegates to
    /// IExportService.ProcessExportAsync, so the tests check what is forwarded and
    /// how exceptions behave. The two things a delegation test must pin down are
    /// the CancellationToken (a lost token would change request cancellation) and
    /// the identity of the returned Task (an async/await added here would hide the real
    /// moment the work completes).
    /// </summary>
    public class InlineExportJobDispatcherTests
    {
        private const int ExportRequestId = 100;

        private readonly Mock<IExportService> _exportService = new();

        private CancellationToken? _capturedToken;
        private Task? _returnedTask;

        [Fact]
        public async Task DispatchAsync_ForwardsTheExportRequestId()
        {
            var capturedId = 0;
            SetupProcessExportAsync(id => capturedId = id);

            await CreateDispatcher().DispatchAsync(ExportRequestId);

            // The id reaches ProcessExportAsync unchanged: it is the only data the dispatcher
            // carries, and the processed export must be the requested one.
            Assert.Equal(ExportRequestId, capturedId);
        }

        [Fact]
        public async Task DispatchAsync_ForwardsTheCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            await dispatcher.DispatchAsync(ExportRequestId, cts.Token);

            // The token crosses the delegation by reference: it is the HTTP request's own,
            // so a client that disconnects also stops the inline export.
            Assert.NotNull(_capturedToken);
            Assert.Equal(cts.Token, _capturedToken.Value);
            Assert.True(_capturedToken.Value.CanBeCanceled);
        }

        [Fact]
        public async Task DispatchAsync_UsesDefaultToken_WhenNoneIsProvided()
        {
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            // Call without a token, as a consumer without a request context
            // to pass might do.
            await dispatcher.DispatchAsync(ExportRequestId);

            // The optional parameter default reaches the service as None, and not as a
            // new token created by the dispatcher: this is the case that tells a faithful
            // delegation apart from one that stops honoring the token it received.
            Assert.NotNull(_capturedToken);
            Assert.Equal(CancellationToken.None, _capturedToken.Value);
            Assert.False(_capturedToken.Value.CanBeCanceled);
        }

        [Fact]
        public void DispatchAsync_ReturnsTheSameTaskInstance_FromTheExportService()
        {
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            // The comparison happens before any await, so the assert proves the identity
            // of the instance and not the final value, which would be the same either way.
            var returned = dispatcher.DispatchAsync(ExportRequestId);

            // No async/await in the dispatcher: the returned Task is literally the one from
            // ProcessExportAsync. If an await were added here one day, the work
            // would not change but the observable completion moment would: the failure of
            // this test makes that decision explicit instead of leaving it implicit.
            Assert.NotNull(_returnedTask);
            Assert.Same(_returnedTask, returned);
        }

        [Fact]
        public async Task DispatchAsync_PropagatesTheFailure_FromTheExportService()
        {
            var expected = new InvalidOperationException("Export processing failed for test purposes");
            SetupProcessExportAsync(_ => throw expected);
            var dispatcher = CreateDispatcher();

            // The export runs inline, so a failure must go back to the same request
            // that asked for it: GlobalExceptionHandler turns it into an HTTP response.
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAsync(ExportRequestId));

            Assert.Same(expected, actual);
        }

        private InlineExportJobDispatcher CreateDispatcher() => new(_exportService.Object);

        private void SetupProcessExportAsync(Action<int>? onCall = null)
        {
            _capturedToken = null;
            _returnedTask = null;

            // A fresh Task, already completed but not a singleton: Task.CompletedTask is a shared
            // instance, so the identity check would pass even if the dispatcher built a
            // new one. SetResult is essential: a TaskCompletionSource stays pending until
            // it is completed, and awaiting it would block the test forever.
            var completion = new TaskCompletionSource();
            completion.SetResult();
            var processing = completion.Task;

            _exportService
                .Setup(s => s.ProcessExportAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, CancellationToken>((id, ct) =>
                {
                    _capturedToken = ct;
                    onCall?.Invoke(id);
                })
                .Returns(() => _returnedTask = processing);
        }
    }
}
