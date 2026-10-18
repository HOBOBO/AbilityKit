#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Game.Flow
{
    internal sealed class MultiplayerAssetLoadingRuntime
    {
        private readonly IMultiplayerBattleAssetLoader? _loader;
        private readonly object _progressGate = new object();
        private CancellationTokenSource? _operationCancellation;
        private int _operationGeneration;
        private int _progress;
        private string _currentAssetKey = string.Empty;

        public MultiplayerAssetLoadingRuntime(IMultiplayerBattleAssetLoader? loader)
        {
            _loader = loader;
        }

        public bool IsAvailable => _loader != null;

        public int Progress
        {
            get { lock (_progressGate) return _progress; }
        }

        public string CurrentAssetKey
        {
            get { lock (_progressGate) return _currentAssetKey; }
        }

        public async Task LoadAsync(
            MultiplayerRoomSnapshot snapshot,
            Func<int, CancellationToken, Task> reportProgressAsync,
            CancellationToken cancellationToken)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (reportProgressAsync == null) throw new ArgumentNullException(nameof(reportProgressAsync));
            if (_loader == null)
            {
                throw new InvalidOperationException(
                    "No multiplayer battle asset loader is registered.");
            }

            var operationCancellation = BeginOperation(
                cancellationToken,
                out var operationGeneration);
            var progressRelay = new ClientLoadingProgressRelay();
            var progressTask = progressRelay.UploadUntilCompletedAsync(
                reportProgressAsync,
                cancellationToken: operationCancellation.Token);
            try
            {
                await _loader.LoadAsync(
                    snapshot,
                    new ImmediateProgress<MultiplayerAssetLoadProgress>(value =>
                    {
                        if (!TryUpdateProgress(operationGeneration, value)) return;
                        progressRelay.Report(new ClientLoadingProgress(
                            value.CurrentAssetKey,
                            value.Progress,
                            value.Progress / 100f));
                    }),
                    operationCancellation.Token).ConfigureAwait(false);
                CompleteProgress(operationGeneration);
                progressRelay.Complete("complete");
                await progressTask.ConfigureAwait(false);
            }
            catch
            {
                TryCancel(operationCancellation);
                try
                {
                    await progressTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
                {
                }

                throw;
            }
            finally
            {
                lock (_progressGate)
                {
                    if (ReferenceEquals(_operationCancellation, operationCancellation))
                    {
                        _operationCancellation = null;
                    }
                }
                operationCancellation.Dispose();
            }
        }

        public void Cancel(bool releaseAssets)
        {
            CancellationTokenSource? cancellation;
            lock (_progressGate)
            {
                cancellation = _operationCancellation;
                _operationCancellation = null;
                _operationGeneration++;
                ResetProgressLocked();
            }
            TryCancel(cancellation);
            if (releaseAssets) _loader?.Release();
        }

        private CancellationTokenSource BeginOperation(
            CancellationToken cancellationToken,
            out int operationGeneration)
        {
            CancellationTokenSource? previousCancellation;
            CancellationTokenSource operationCancellation;
            lock (_progressGate)
            {
                previousCancellation = _operationCancellation;
                operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _operationCancellation = operationCancellation;
                _operationGeneration++;
                operationGeneration = _operationGeneration;
                ResetProgressLocked();
            }
            TryCancel(previousCancellation);
            return operationCancellation;
        }

        private static void TryCancel(CancellationTokenSource? cancellation)
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private bool TryUpdateProgress(
            int operationGeneration,
            MultiplayerAssetLoadProgress progress)
        {
            lock (_progressGate)
            {
                if (operationGeneration != _operationGeneration || progress.Progress < _progress)
                {
                    return false;
                }

                _progress = progress.Progress;
                _currentAssetKey = progress.CurrentAssetKey;
                return true;
            }
        }

        private void CompleteProgress(int operationGeneration)
        {
            lock (_progressGate)
            {
                if (operationGeneration == _operationGeneration)
                {
                    _progress = 100;
                }
            }
        }

        private void ResetProgressLocked()
        {
            _progress = 0;
            _currentAssetKey = string.Empty;
        }

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public ImmediateProgress(Action<T> report)
            {
                _report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public void Report(T value) => _report(value);
        }
    }
}
