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

    public sealed class GatewayPushOperationRuntime : IDisposable
    {
        private readonly object _gate = new object();
        private readonly System.Collections.Generic.HashSet<Task> _pending =
            new System.Collections.Generic.HashSet<Task>();
        private CancellationTokenSource? _lifetime;
        private Func<uint, ArraySegment<byte>, CancellationToken, Task>? _handler;
        private Action<Exception>? _failure;
        private int _attachmentGeneration;
        private bool _disposed;

        public bool IsAttached
        {
            get
            {
                lock (_gate)
                {
                    return _lifetime != null;
                }
            }
        }

        public Task PendingTask
        {
            get
            {
                lock (_gate)
                {
                    return SnapshotPendingLocked();
                }
            }
        }

        public void Attach(
            Func<uint, ArraySegment<byte>, CancellationToken, Task> handler,
            Action<Exception>? failure = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            CancellationTokenSource? previousLifetime;
            lock (_gate)
            {
                ThrowIfDisposed();
                previousLifetime = DetachLocked();
                _attachmentGeneration++;
                _handler = handler;
                _failure = failure;
                _lifetime = new CancellationTokenSource();
            }

            CancelAndDispose(previousLifetime);
        }

        public bool TryStart(uint opCode, ArraySegment<byte> payload)
        {
            Task operationTask;
            lock (_gate)
            {
                if (_disposed || _lifetime == null || _handler == null)
                {
                    return false;
                }

                var operation = new GatewayPushOperationContext(
                    _attachmentGeneration,
                    _lifetime.Token,
                    _handler,
                    _failure);
                operationTask = RunOperationAsync(operation, opCode, payload);
                _pending.Add(operationTask);
                _ = operationTask.ContinueWith(
                    completed => RemovePending(completed),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return true;
        }

        public Task Detach()
        {
            CancellationTokenSource? lifetime;
            Task pending;
            lock (_gate)
            {
                lifetime = DetachLocked();
                _attachmentGeneration++;
                pending = SnapshotPendingLocked();
            }

            CancelAndDispose(lifetime);
            return pending;
        }

        public void Dispose()
        {
            CancellationTokenSource? lifetime;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                lifetime = DetachLocked();
                _attachmentGeneration++;
            }

            CancelAndDispose(lifetime);
        }

        private async Task RunOperationAsync(
            GatewayPushOperationContext operation,
            uint opCode,
            ArraySegment<byte> payload)
        {
            try
            {
                await operation.Handler(
                        opCode,
                        payload,
                        operation.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (operation.CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!IsCurrent(operation.AttachmentGeneration)) return;

                try
                {
                    operation.Failure?.Invoke(exception);
                }
                catch
                {
                }
            }
        }

        private bool IsCurrent(int attachmentGeneration)
        {
            lock (_gate)
            {
                return !_disposed &&
                       _lifetime != null &&
                       _attachmentGeneration == attachmentGeneration;
            }
        }

        private void RemovePending(Task completed)
        {
            lock (_gate)
            {
                _pending.Remove(completed);
            }
        }

        private CancellationTokenSource? DetachLocked()
        {
            var lifetime = _lifetime;
            _lifetime = null;
            _handler = null;
            _failure = null;
            return lifetime;
        }

        private Task SnapshotPendingLocked()
        {
            if (_pending.Count == 0) return Task.CompletedTask;

            var pending = new Task[_pending.Count];
            _pending.CopyTo(pending);
            return Task.WhenAll(pending);
        }

        private static void CancelAndDispose(CancellationTokenSource? lifetime)
        {
            if (lifetime == null) return;

            try
            {
                lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                lifetime.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GatewayPushOperationRuntime));
            }
        }

        private readonly struct GatewayPushOperationContext
        {
            public GatewayPushOperationContext(
                int attachmentGeneration,
                CancellationToken cancellationToken,
                Func<uint, ArraySegment<byte>, CancellationToken, Task> handler,
                Action<Exception>? failure)
            {
                AttachmentGeneration = attachmentGeneration;
                CancellationToken = cancellationToken;
                Handler = handler;
                Failure = failure;
            }

            public int AttachmentGeneration { get; }
            public CancellationToken CancellationToken { get; }
            public Func<uint, ArraySegment<byte>, CancellationToken, Task> Handler { get; }
            public Action<Exception>? Failure { get; }
        }
    }

    public sealed class MultiplayerGatewayEntryRuntime : IDisposable
    {
        private readonly object _gate = new object();
        private MultiplayerGatewayEntryAttachment? _attachment;
        private Task _pendingTask = Task.CompletedTask;
        private int _attachmentGeneration;
        private bool _disposed;

        public bool IsAttached
        {
            get
            {
                lock (_gate)
                {
                    return _attachment != null;
                }
            }
        }

        public int AttachmentGeneration
        {
            get
            {
                lock (_gate)
                {
                    return _attachmentGeneration;
                }
            }
        }

        public CancellationToken LifetimeToken
        {
            get
            {
                lock (_gate)
                {
                    return _attachment?.LifetimeToken ?? new CancellationToken(canceled: true);
                }
            }
        }

        public Task PendingTask
        {
            get
            {
                lock (_gate)
                {
                    return _pendingTask;
                }
            }
        }

        public void Attach(Action<MultiplayerGatewayEntryAttachment> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            MultiplayerGatewayEntryAttachment attachment;
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_attachment != null)
                {
                    throw new InvalidOperationException("The gateway entry runtime is already attached.");
                }

                if (!_pendingTask.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The previous gateway entry teardown is still running.");
                }

                _pendingTask = Task.CompletedTask;
                _attachmentGeneration++;
                attachment = new MultiplayerGatewayEntryAttachment(_attachmentGeneration);
                _attachment = attachment;
            }

            try
            {
                configure(attachment);
            }
            catch
            {
                DetachAttachment(attachment);
                throw;
            }
        }

        public bool IsCurrent(int attachmentGeneration)
        {
            lock (_gate)
            {
                return !_disposed &&
                       _attachment != null &&
                       _attachmentGeneration == attachmentGeneration;
            }
        }

        public Task Detach()
        {
            MultiplayerGatewayEntryAttachment? attachment;
            lock (_gate)
            {
                attachment = _attachment;
            }

            return attachment == null
                ? PendingTask
                : DetachAttachment(attachment);
        }

        public void Dispose()
        {
            MultiplayerGatewayEntryAttachment? attachment;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                attachment = _attachment;
            }

            if (attachment != null)
            {
                DetachAttachment(attachment);
            }
        }

        private Task DetachAttachment(MultiplayerGatewayEntryAttachment attachment)
        {
            Task teardown;
            lock (_gate)
            {
                if (!ReferenceEquals(_attachment, attachment))
                {
                    return _pendingTask;
                }

                _attachment = null;
                _attachmentGeneration++;
                teardown = attachment.Detach();
                _pendingTask = CombinePending(_pendingTask, teardown);
                return _pendingTask;
            }
        }

        private static Task CombinePending(Task previous, Task current)
        {
            if (previous.IsCompletedSuccessfully) return current;
            if (current.IsCompletedSuccessfully) return previous;
            return Task.WhenAll(previous, current);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MultiplayerGatewayEntryRuntime));
            }
        }
    }

    public sealed class MultiplayerGatewayEntryAttachment
    {
        private readonly object _gate = new object();
        private readonly System.Collections.Generic.List<Func<Task>> _teardown =
            new System.Collections.Generic.List<Func<Task>>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _detached;

        internal MultiplayerGatewayEntryAttachment(int generation)
        {
            Generation = generation;
        }

        public int Generation { get; }
        public CancellationToken LifetimeToken => _lifetime.Token;

        public void Register(Action teardown)
        {
            if (teardown == null) throw new ArgumentNullException(nameof(teardown));
            Register(() =>
            {
                teardown();
                return Task.CompletedTask;
            });
        }

        public void Register(Func<Task> teardown)
        {
            if (teardown == null) throw new ArgumentNullException(nameof(teardown));

            lock (_gate)
            {
                if (_detached)
                {
                    throw new InvalidOperationException("Cannot register teardown after detachment.");
                }

                _teardown.Add(teardown);
            }
        }

        internal Task Detach()
        {
            Func<Task>[] teardown;
            lock (_gate)
            {
                if (_detached) return Task.CompletedTask;
                _detached = true;
                teardown = _teardown.ToArray();
                _teardown.Clear();
            }

            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            return RunTeardownAsync(teardown, _lifetime);
        }

        private static async Task RunTeardownAsync(
            Func<Task>[] teardown,
            CancellationTokenSource lifetime)
        {
            var failures = new System.Collections.Generic.List<Exception>();
            try
            {
                for (var index = teardown.Length - 1; index >= 0; index--)
                {
                    try
                    {
                        var task = teardown[index]();
                        if (task != null)
                        {
                            await task.ConfigureAwait(false);
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                }

                if (failures.Count == 1) throw failures[0];
                if (failures.Count > 1) throw new AggregateException(failures);
            }
            finally
            {
                lifetime.Dispose();
            }
        }
    }
}
