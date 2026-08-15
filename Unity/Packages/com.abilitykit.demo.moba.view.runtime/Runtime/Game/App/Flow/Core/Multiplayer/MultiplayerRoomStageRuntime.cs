#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    internal sealed class MultiplayerRoomStageRuntime : IDisposable
    {
        private readonly object _gate = new object();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CancellationTokenSource? _stageCancellation;
        private Task _stageTask = Task.CompletedTask;
        private long _generation = -1;
        private bool _disposed;

        public Task ResumeAsync(
            long generation,
            Func<CancellationToken, Task> runStage,
            CancellationToken cancellationToken = default)
        {
            if (runStage == null) throw new ArgumentNullException(nameof(runStage));

            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_stageTask.IsCompleted && _generation == generation)
                {
                    return _stageTask;
                }

                var previousTask = _stageTask;
                CancelLocked();
                _generation = generation;
                _stageCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetime.Token,
                    cancellationToken);
                _stageTask = RunAfterPreviousAsync(
                    previousTask,
                    runStage,
                    _stageCancellation.Token);
                return _stageTask;
            }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                CancelLocked();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _lifetime.Cancel();
                CancelLocked();
                _lifetime.Dispose();
            }
        }

        private static async Task RunAfterPreviousAsync(
            Task previousTask,
            Func<CancellationToken, Task> runStage,
            CancellationToken cancellationToken)
        {
            try
            {
                await previousTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // A newer authoritative launch generation supersedes the previous failure.
            }

            cancellationToken.ThrowIfCancellationRequested();
            await runStage(cancellationToken).ConfigureAwait(false);
        }

        private void CancelLocked()
        {
            try
            {
                _stageCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _stageCancellation?.Dispose();
            _stageCancellation = null;
            _generation = -1;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MultiplayerRoomStageRuntime));
        }
    }
}
