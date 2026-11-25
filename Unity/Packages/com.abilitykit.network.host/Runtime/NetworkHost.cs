#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Timing;

namespace AbilityKit.Network.Host
{
    /// <summary>Owns a listener and all accepted framed sessions.</summary>
    public sealed class NetworkHost : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IChannelListener _listener;
        private readonly NetworkHostOptions _options;
        private readonly IMonotonicClock _clock;
        private readonly Dictionary<string, ServerNetworkSession> _sessions = new Dictionary<string, ServerNetworkSession>();
        private readonly Dictionary<string, RequestQueue> _requestQueues = new Dictionary<string, RequestQueue>();
        private readonly HashSet<string> _closingSessions = new HashSet<string>();
        private volatile bool _started;
        private volatile bool _disposed;
        private bool _starting;
        private TaskCompletionSource<bool>? _stopCompletion;
        private long _acceptedSessions;
        private long _closedSessions;
        private long _rejectedSessions;
        private long _idleTimeouts;
        private long _listenerErrors;
        private long _sessionErrors;
        private long _requestsQueued;
        private long _requestsCompleted;
        private long _requestsFailed;
        private long _requestsRejected;
        private long _requestsCancelled;
        private long _establishmentTimeouts;
        private long _admissionRejections;
        private long _gracefulStops;
        private long _drainTimeouts;

        public NetworkHost(IChannelListener listener, NetworkHostOptions options = null)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _options = options ?? new NetworkHostOptions();
            if (_options.MaxConnections <= 0) throw new ArgumentOutOfRangeException(nameof(options), "MaxConnections must be positive.");
            if (_options.MaxPendingRequestsPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxPendingRequestsPerSession must be positive.");
            if (_options.IdleTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), "IdleTimeout cannot be negative.");
            if (_options.EstablishmentTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options), "EstablishmentTimeout cannot be negative.");
            if (_options.RequestHandler != null && _options.AsyncRequestHandler != null)
                throw new ArgumentException("Configure either RequestHandler or AsyncRequestHandler, not both.", nameof(options));
            _clock = _options.Clock ?? StopwatchMonotonicClock.Instance;
            if (_clock.Frequency <= 0) throw new ArgumentException("Clock frequency must be positive.", nameof(options));
        }

        public bool IsListening => !_disposed && _listener.IsListening;
        public string Endpoint => _listener.Endpoint;
        public int SessionCount { get { lock (_gate) return _sessions.Count; } }

        public NetworkHostDiagnostics GetDiagnostics()
        {
            return new NetworkHostDiagnostics(
                SessionCount,
                Interlocked.Read(ref _acceptedSessions),
                Interlocked.Read(ref _closedSessions),
                Interlocked.Read(ref _rejectedSessions),
                Interlocked.Read(ref _idleTimeouts),
                Interlocked.Read(ref _listenerErrors),
                Interlocked.Read(ref _sessionErrors),
                Interlocked.Read(ref _requestsQueued),
                Interlocked.Read(ref _requestsCompleted),
                Interlocked.Read(ref _requestsFailed),
                Interlocked.Read(ref _requestsRejected),
                Interlocked.Read(ref _requestsCancelled),
                Interlocked.Read(ref _establishmentTimeouts),
                Interlocked.Read(ref _admissionRejections),
                Interlocked.Read(ref _gracefulStops),
                Interlocked.Read(ref _drainTimeouts));
        }

        public IReadOnlyList<NetworkHostSessionSnapshot> GetSessionSnapshots()
        {
            lock (_gate)
            {
                var snapshots = new List<NetworkHostSessionSnapshot>(_sessions.Count);
                foreach (var pair in _sessions)
                {
                    var session = pair.Value;
                    _requestQueues.TryGetValue(pair.Key, out var queue);
                    snapshots.Add(new NetworkHostSessionSnapshot(
                        session.Id,
                        session.Channel.RemoteEndpoint,
                        session.IsConnected,
                        session.Context.IsEstablished,
                        session.OpenedTimestamp,
                        session.LastActivityTimestamp,
                        _clock.Frequency,
                        queue?.PendingCount ?? 0,
                        session.BytesReceivedCount,
                        session.BytesSentCount,
                        session.PacketsReceivedCount,
                        session.PacketsSentCount));
                }
                return snapshots;
            }
        }

        public event Action<IServerNetworkSession> SessionOpened;
        public event Action<IServerNetworkSession> SessionClosed;
        public event Action<IServerNetworkSession, Exception> SessionError;
        public event Action<IServerNetworkSession, Protocol.NetworkPacketHeader, ArraySegment<byte>> PacketReceived;
        public event Action<Exception> ListenerError;
        public event Action<IServerChannel, ChannelAdmissionResult> ChannelRejected;

        /// <summary>
        /// 开始监听。只能在上一次停机完成后重新启动。
        /// </summary>
        public void Start()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                ThrowIfLifecycleChanging();
                if (_started) throw new InvalidOperationException("Network host is already running.");
                _starting = true;
                _started = true;
            }
            try
            {
                _listener.ChannelAccepted += OnChannelAccepted;
                _listener.Error += OnListenerError;
                _listener.Start();
            }
            catch
            {
                _started = false;
                _listener.ChannelAccepted -= OnChannelAccepted;
                _listener.Error -= OnListenerError;
                try { _listener.Stop(); }
                catch { }
                CloseAllSessions();
                throw;
            }
            finally
            {
                lock (_gate) _starting = false;
            }
        }

        /// <summary>
        /// 停止接入并立即取消请求、关闭会话；启动或停机尚未完成时不允许调用。
        /// </summary>
        public void Stop()
        {
            TaskCompletionSource<bool> stop;
            lock (_gate)
            {
                ThrowIfLifecycleChanging();
                if (!_started) return;
                stop = BeginStop();
            }

            StopImmediately(stop);
        }

        /// <summary>
        /// 停止接入，等待已入队请求，并在返回前关闭本次运行的会话。
        /// 停机未完成时，后续调用等待同一个结果，不替换首次调用的排空期限或取消令牌。
        /// </summary>
        /// <param name="drainTimeout">等待已有请求完成的期限，零表示不等待。</param>
        /// <param name="cancellationToken">取消本次排空等待；会话清理仍会执行。</param>
        public async Task StopAsync(TimeSpan drainTimeout, CancellationToken cancellationToken = default)
        {
            if (drainTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(drainTimeout), "Drain timeout cannot be negative.");

            TaskCompletionSource<bool>? ownedStop = null;
            Task completion;
            lock (_gate)
            {
                if (_stopCompletion != null)
                {
                    completion = _stopCompletion.Task;
                }
                else
                {
                    ThrowIfLifecycleChanging();
                    if (!_started) return;
                    ownedStop = BeginStop();
                    completion = ownedStop.Task;
                }
            }

            if (ownedStop != null) _ = CompleteStopAsync(ownedStop, drainTimeout, cancellationToken);
            await completion.ConfigureAwait(false);
        }

        private TaskCompletionSource<bool> BeginStop()
        {
            _started = false;
            _stopCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _stopCompletion;
        }

        private void StopImmediately(TaskCompletionSource<bool> stop)
        {
            _ = CompleteStopAsync(stop, null, default);
            stop.Task.GetAwaiter().GetResult();
        }

        private async Task CompleteStopAsync(
            TaskCompletionSource<bool> stop,
            TimeSpan? drainTimeout,
            CancellationToken cancellationToken)
        {
            Exception? error = null;
            try
            {
                await StopCoreAsync(drainTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                error = exception;
            }

            lock (_gate)
            {
                _stopCompletion = null;
                if (error is OperationCanceledException cancelled && drainTimeout.HasValue)
                    stop.TrySetCanceled(cancelled.CancellationToken);
                else if (error != null)
                    stop.SetException(error);
                else
                    stop.SetResult(true);
            }
        }

        private async Task StopCoreAsync(TimeSpan? drainTimeout, CancellationToken cancellationToken)
        {
            Exception? listenerStopError = null;
            try
            {
                _listener.ChannelAccepted -= OnChannelAccepted;
                _listener.Error -= OnListenerError;
                try { _listener.Stop(); }
                catch (Exception exception) when (drainTimeout.HasValue) { listenerStopError = exception; }

                if (drainTimeout.HasValue)
                {
                    if (await DrainRequestsAsync(drainTimeout.Value, cancellationToken).ConfigureAwait(false))
                        Interlocked.Increment(ref _gracefulStops);
                    else
                        Interlocked.Increment(ref _drainTimeouts);
                }
            }
            finally
            {
                CloseAllSessions();
            }

            if (listenerStopError != null) throw listenerStopError;
        }

        private async Task<bool> DrainRequestsAsync(
            TimeSpan drainTimeout,
            CancellationToken cancellationToken)
        {
            Task[] drains;
            lock (_gate)
            {
                drains = new Task[_requestQueues.Count];
                var index = 0;
                foreach (var queue in _requestQueues.Values) drains[index++] = queue.BeginDrain();
            }

            var allDrained = Task.WhenAll(drains);
            if (allDrained.IsCompleted)
            {
                await allDrained.ConfigureAwait(false);
                return true;
            }
            if (drainTimeout == TimeSpan.Zero) return false;

            var timeout = Task.Delay(drainTimeout, cancellationToken);
            var completed = await Task.WhenAny(allDrained, timeout).ConfigureAwait(false);
            if (completed == allDrained)
            {
                await allDrained.ConfigureAwait(false);
                return true;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        private void CloseAllSessions()
        {
            ServerNetworkSession[] sessions;
            lock (_gate)
            {
                sessions = new ServerNetworkSession[_sessions.Count];
                _sessions.Values.CopyTo(sessions, 0);
                _sessions.Clear();
                foreach (var queue in _requestQueues.Values) queue.Cancel();
                _requestQueues.Clear();
                _closingSessions.Clear();
            }

            foreach (var session in sessions)
            {
                Unsubscribe(session);
                session.Dispose();
            }
        }

        /// <summary>Runs host maintenance. Call from Unity Update or a headless host loop.</summary>
        public void Tick()
        {
            if (!_started ||
                (_options.IdleTimeout <= TimeSpan.Zero && _options.EstablishmentTimeout <= TimeSpan.Zero)) return;
            var now = _clock.Timestamp;
            var timeoutTicks = MonotonicTime.DurationToTimestampTicks(_options.IdleTimeout, _clock.Frequency);
            var establishmentTicks = MonotonicTime.DurationToTimestampTicks(_options.EstablishmentTimeout, _clock.Frequency);
            ServerNetworkSession[] sessions;
            lock (_gate)
            {
                sessions = new ServerNetworkSession[_sessions.Count];
                _sessions.Values.CopyTo(sessions, 0);
            }
            foreach (var session in sessions)
            {
                if (!session.IsConnected) continue;
                var establishmentExpired =
                    establishmentTicks > 0 &&
                    !session.Context.IsEstablished &&
                    now - session.OpenedTimestamp >= establishmentTicks;
                var idleExpired =
                    timeoutTicks > 0 &&
                    now - session.LastActivityTimestamp >= timeoutTicks;
                if (!establishmentExpired && !idleExpired) continue;
                if (!TryBeginClosing(session.Id)) continue;

                if (establishmentExpired) Interlocked.Increment(ref _establishmentTimeouts);
                else Interlocked.Increment(ref _idleTimeouts);
                CloseSession(session);
            }
        }

        public bool TryGetSession(string id, out IServerNetworkSession session)
        {
            lock (_gate)
            {
                if (_sessions.TryGetValue(id, out var concrete))
                {
                    session = concrete;
                    return true;
                }
            }
            session = null;
            return false;
        }

        /// <summary>
        /// 关闭会话并释放监听器；启动或停机尚未完成时不允许调用。
        /// </summary>
        public void Dispose()
        {
            TaskCompletionSource<bool>? stop;
            lock (_gate)
            {
                ThrowIfLifecycleChanging();
                if (_disposed) return;
                _disposed = true;
                stop = _started ? BeginStop() : null;
            }
            try
            {
                if (stop != null) StopImmediately(stop);
            }
            finally
            {
                _listener.Dispose();
            }
        }

        private void OnChannelAccepted(IServerChannel channel)
        {
            if (channel == null) return;

            if (_options.AdmissionPolicy != null)
            {
                ChannelAdmissionResult admission;
                try
                {
                    admission = _options.AdmissionPolicy.Evaluate(channel, SessionCount);
                }
                catch (Exception exception)
                {
                    OnListenerError(exception);
                    admission = ChannelAdmissionResult.Reject("admission-policy-error");
                }
                if (!admission.Accepted)
                {
                    if (string.IsNullOrWhiteSpace(admission.Reason))
                        admission = ChannelAdmissionResult.Reject("admission-policy-rejected");
                    Interlocked.Increment(ref _admissionRejections);
                    RejectChannel(channel, admission);
                    return;
                }
            }

            ServerNetworkSession session = null;
            string rejectionReason = null;
            lock (_gate)
            {
                if (!_started) rejectionReason = "host-not-listening";
                else if (_sessions.Count >= _options.MaxConnections) rejectionReason = "max-connections";
                else if (_sessions.ContainsKey(channel.Id)) rejectionReason = "duplicate-channel-id";
                else
                {
                    session = new ServerNetworkSession(
                        channel,
                        _options.CallbackDispatcher,
                        _options.IoDispatcher,
                        _options.FrameCodec,
                        _clock,
                        _options.MaxFrameLength);
                    _options.ConfigurePipeline?.Invoke(new NetworkPipelineBuilder(session.Pipeline));
                    _sessions.Add(session.Id, session);
                    _requestQueues.Add(
                        session.Id,
                        new RequestQueue(
                            _options.MaxPendingRequestsPerSession,
                            OnRequestCompleted,
                            exception => OnRequestFailed(session, exception),
                            OnRequestCancelled));
                }
            }

            if (rejectionReason != null)
            {
                RejectChannel(channel, ChannelAdmissionResult.Reject(rejectionReason));
                return;
            }

            Interlocked.Increment(ref _acceptedSessions);

            session.Closed += OnSessionClosed;
            session.SessionError += OnSessionError;
            session.RequestReceived += OnPacketReceived;
            session.Start();
            SessionOpened?.Invoke(session);
        }

        private void OnSessionClosed(IServerNetworkSession session)
        {
            ServerNetworkSession concrete = null;
            lock (_gate)
            {
                if (_sessions.TryGetValue(session.Id, out concrete)) _sessions.Remove(session.Id);
                _closingSessions.Remove(session.Id);
                if (_requestQueues.TryGetValue(session.Id, out var queue))
                {
                    _requestQueues.Remove(session.Id);
                    queue.Cancel();
                }
            }
            if (concrete == null) return;
            Unsubscribe(concrete);
            Interlocked.Increment(ref _closedSessions);
            SessionClosed?.Invoke(concrete);
            concrete.Dispose();
        }

        private void OnSessionError(IServerNetworkSession session, Exception exception)
        {
            Interlocked.Increment(ref _sessionErrors);
            SessionError?.Invoke(session, exception);
        }

        private void OnPacketReceived(
            IServerNetworkSession session,
            Protocol.NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            PacketReceived?.Invoke(session, header, payload);
            if (!_started) return;
            if (_options.RequestHandler == null && _options.AsyncRequestHandler == null) return;

            RequestQueue queue;
            lock (_gate)
            {
                if (!_requestQueues.TryGetValue(session.Id, out queue)) return;
            }
            if (!queue.TryEnqueue(token => HandleRequestAsync(session, header, payload, token)))
            {
                if (!_started) return;
                Interlocked.Increment(ref _requestsRejected);
                if (TryBeginClosing(session.Id)) CloseSession(session);
                return;
            }
            Interlocked.Increment(ref _requestsQueued);
        }

        private void OnListenerError(Exception exception)
        {
            Interlocked.Increment(ref _listenerErrors);
            ListenerError?.Invoke(exception);
        }

        private Task HandleRequestAsync(
            IServerNetworkSession session,
            Protocol.NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken)
        {
            if (_options.AsyncRequestHandler != null)
            {
                return _options.AsyncRequestHandler.HandleAsync(session, header, payload, cancellationToken)
                    ?? Task.FromException(new InvalidOperationException("Async request handlers must return a Task."));
            }
            _options.RequestHandler.Handle(session, header, payload);
            return Task.CompletedTask;
        }

        private void OnRequestCompleted()
        {
            Interlocked.Increment(ref _requestsCompleted);
        }

        private void OnRequestFailed(IServerNetworkSession session, Exception exception)
        {
            Interlocked.Increment(ref _requestsFailed);
            Interlocked.Increment(ref _sessionErrors);
            SessionError?.Invoke(session, exception);
        }

        private void OnRequestCancelled()
        {
            Interlocked.Increment(ref _requestsCancelled);
        }

        private void RejectChannel(IServerChannel channel, ChannelAdmissionResult result)
        {
            Interlocked.Increment(ref _rejectedSessions);
            try
            {
                ChannelRejected?.Invoke(channel, result);
            }
            finally
            {
                channel.Dispose();
            }
        }

        private bool TryBeginClosing(string sessionId)
        {
            lock (_gate)
            {
                return _sessions.ContainsKey(sessionId) && _closingSessions.Add(sessionId);
            }
        }

        private void CloseSession(IServerNetworkSession session)
        {
            try
            {
                session.Close();
            }
            catch (Exception exception)
            {
                OnSessionError(session, exception);
                OnSessionClosed(session);
            }
        }

        private void Unsubscribe(ServerNetworkSession session)
        {
            session.Closed -= OnSessionClosed;
            session.SessionError -= OnSessionError;
            session.RequestReceived -= OnPacketReceived;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkHost));
        }

        private void ThrowIfLifecycleChanging()
        {
            if (_starting || _stopCompletion != null)
                throw new InvalidOperationException("Wait for the active start or stop before changing the network host lifecycle.");
        }

        private sealed class RequestQueue
        {
            private readonly object _sync = new object();
            private readonly int _maxPending;
            private readonly Action _completed;
            private readonly Action<Exception> _failed;
            private readonly Action _cancelled;
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private Task _tail = Task.CompletedTask;
            private int _pending;
            private bool _accepting = true;

            public RequestQueue(
                int maxPending,
                Action completed,
                Action<Exception> failed,
                Action cancelled)
            {
                _maxPending = maxPending;
                _completed = completed;
                _failed = failed;
                _cancelled = cancelled;
            }

            public int PendingCount => Volatile.Read(ref _pending);

            public bool TryEnqueue(Func<CancellationToken, Task> request)
            {
                lock (_sync)
                {
                    if (!_accepting || _cancellation.IsCancellationRequested || _pending >= _maxPending) return false;
                    _pending++;
                    _tail = RunAfterAsync(_tail, request, _cancellation.Token);
                    return true;
                }
            }

            public void Cancel()
            {
                lock (_sync)
                {
                    _accepting = false;
                    if (!_cancellation.IsCancellationRequested) _cancellation.Cancel();
                }
            }

            public Task BeginDrain()
            {
                lock (_sync)
                {
                    _accepting = false;
                    return _tail;
                }
            }

            private async Task RunAfterAsync(
                Task previous,
                Func<CancellationToken, Task> request,
                CancellationToken cancellationToken)
            {
                try
                {
                    await previous.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    await request(cancellationToken).ConfigureAwait(false);
                    _completed();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _cancelled();
                }
                catch (Exception exception)
                {
                    _failed(exception);
                }
                finally
                {
                    lock (_sync) _pending--;
                }
            }
        }
    }
}
