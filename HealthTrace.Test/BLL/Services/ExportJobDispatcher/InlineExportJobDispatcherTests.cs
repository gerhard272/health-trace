using HealthTrace.BLL.Services;
using HealthTrace.BLL.Services.Interfaces;
using Moq;

namespace HealthTrace.Test.BLL.Services.ExportJobDispatcher
{
    /// <summary>
    /// Copre InlineExportJobDispatcher, l'implementazione che esegue l'export nella stessa
    /// richiesta HTTP che lo ha richiesto. Non ha logica propria: e' una delega a
    /// IExportService.ProcessExportAsync, quindi i test verificano cosa viene inoltrato e
    /// come si comportano le eccezioni. I due punti che un test di delega deve fissare sono
    /// il CancellationToken (un token perso cambierebbe la cancellazione della richiesta) e
    /// l'identita' della Task restituita (un async/await aggiunto qui mascherebbe il momento
    /// vero di completamento del lavoro).
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

            // L'id arriva a ProcessExportAsync invariato: e' l'unico dato che il dispatcher
            // trasporta, e l'export elaborato deve essere quello richiesto.
            Assert.Equal(ExportRequestId, capturedId);
        }

        [Fact]
        public async Task DispatchAsync_ForwardsTheCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            await dispatcher.DispatchAsync(ExportRequestId, cts.Token);

            // Il token attraversa la delega per reference: e' lo stesso della richiesta HTTP,
            // quindi un client che disconnette interrompe anche l'export inline.
            Assert.NotNull(_capturedToken);
            Assert.Equal(cts.Token, _capturedToken.Value);
            Assert.True(_capturedToken.Value.CanBeCanceled);
        }

        [Fact]
        public async Task DispatchAsync_UsesDefaultToken_WhenNoneIsProvided()
        {
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            // Chiamata senza token, come la puo' fare un consumer che non ha un contesto di
            // richiesta da passare.
            await dispatcher.DispatchAsync(ExportRequestId);

            // Il default del parametro opzionale arriva al servizio come None, e non come un
            // token nuovo creato dal dispatcher: e' questo il caso che distingue una delega
            // vera da una che smette di essere fedele al token ricevuto.
            Assert.NotNull(_capturedToken);
            Assert.Equal(CancellationToken.None, _capturedToken.Value);
            Assert.False(_capturedToken.Value.CanBeCanceled);
        }

        [Fact]
        public void DispatchAsync_ReturnsTheSameTaskInstance_FromTheExportService()
        {
            SetupProcessExportAsync();
            var dispatcher = CreateDispatcher();

            // Il confronto avviene prima di qualunque await, cosi' l'assert prova l'identita'
            // dell'istanza e non il valore finale, che sarebbe lo stesso in ogni caso.
            var returned = dispatcher.DispatchAsync(ExportRequestId);

            // Nessun async/await nel dispatcher: la Task restituita e' letteralmente quella di
            // ProcessExportAsync. Se un domani qui dentro venisse aggiunto un await, il lavoro
            // non cambierebbe ma il momento di completamento osservabile si: il fallimento di
            // questo test rende esplicita quella decisione invece di lasciarla implicita.
            Assert.NotNull(_returnedTask);
            Assert.Same(_returnedTask, returned);
        }

        [Fact]
        public async Task DispatchAsync_PropagatesTheFailure_FromTheExportService()
        {
            var expected = new InvalidOperationException("Export processing failed for test purposes");
            SetupProcessExportAsync(_ => throw expected);
            var dispatcher = CreateDispatcher();

            // L'export gira inline, quindi un fallimento deve tornare alla stessa richiesta
            // che lo ha chiesto: GlobalExceptionHandler la traduce in risposta HTTP.
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAsync(ExportRequestId));

            Assert.Same(expected, actual);
        }

        private InlineExportJobDispatcher CreateDispatcher() => new(_exportService.Object);

        private void SetupProcessExportAsync(Action<int>? onCall = null)
        {
            _capturedToken = null;
            _returnedTask = null;

            // Task fresh, gia' completata ma non singleton: Task.CompletedTask e' un'istanza
            // condivisa, quindi l'identita' passerebbe anche se il dispatcher ne costruisse una
            // nuova. SetResult e' indispensabile: una TaskCompletionSource resta pending finche'
            // non viene completata, e un await su di lei bloccherebbe il test per sempre.
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
