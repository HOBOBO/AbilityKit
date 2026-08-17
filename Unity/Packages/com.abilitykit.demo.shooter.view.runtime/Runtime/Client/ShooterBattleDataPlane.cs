#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Battle;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterBattleDataPlaneOptions
    {
        public static ShooterBattleDataPlaneOptions Default { get; } = new ShooterBattleDataPlaneOptions(
            maxPushesPerDrain: 8,
            maxDrainDuration: TimeSpan.FromMilliseconds(2));

        public ShooterBattleDataPlaneOptions(int maxPushesPerDrain, TimeSpan maxDrainDuration)
        {
            if (maxPushesPerDrain <= 0) throw new ArgumentOutOfRangeException(nameof(maxPushesPerDrain));
            if (maxDrainDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDrainDuration));

            MaxPushesPerDrain = maxPushesPerDrain;
            MaxDrainDuration = maxDrainDuration;
        }

        public int MaxPushesPerDrain { get; }

        public TimeSpan MaxDrainDuration { get; }
    }

    public readonly struct ShooterBattleDataPlaneDiagnostics
    {
        public ShooterBattleDataPlaneDiagnostics(
            int queueDepth,
            int peakQueueDepth,
            long enqueuedPushCount,
            long processedPushCount,
            long drainCount,
            long budgetLimitedDrainCount,
            int lastDrainProcessedCount,
            double lastDrainMilliseconds)
        {
            QueueDepth = queueDepth;
            PeakQueueDepth = peakQueueDepth;
            EnqueuedPushCount = enqueuedPushCount;
            ProcessedPushCount = processedPushCount;
            DrainCount = drainCount;
            BudgetLimitedDrainCount = budgetLimitedDrainCount;
            LastDrainProcessedCount = lastDrainProcessedCount;
            LastDrainMilliseconds = lastDrainMilliseconds;
        }

        public int QueueDepth { get; }

        public int PeakQueueDepth { get; }

        public long EnqueuedPushCount { get; }

        public long ProcessedPushCount { get; }

        public long DrainCount { get; }

        public long BudgetLimitedDrainCount { get; }

        public int LastDrainProcessedCount { get; }

        public double LastDrainMilliseconds { get; }
    }

    /// <summary>
    /// Shooter's battle data-plane push/reconnect/ack dispatcher, driven by the battle
    /// <see cref="NetworkTransport"/>. Subscribes <see cref="NetworkTransport.RawServerPushReceived"/>
    /// (raw opCode,payload, fired BEFORE typed decoding) and feeds the existing
    /// <c>ShooterClientSession.ApplyGatewayPush</c> pipeline, then runs the reliable-event ack and
    /// full-snapshot-resync side-channels. Replaces the push/side-channel logic that lived in
    /// <see cref="ShooterRoomGatewayConnection"/> (P2.2). Borrows the launcher's transport — owns no socket.
    /// </summary>
    /// <remarks>
    /// Ack/resync still execute through <c>ShooterClientBattleHandle</c> (room-gateway RPC). That is safe in
    /// the two-connection topology because the server scopes the reliable-event observer by
    /// <c>accountId:roomId</c> (not by connection), so an ack over the room connection updates the same
    /// observer the battle connection receives pushes for.
    /// </remarks>
    public sealed class ShooterBattleDataPlane : IDisposable
    {
        private static readonly TimeSpan AutomaticFullStateSyncTimeout = TimeSpan.FromSeconds(10);

        private readonly NetworkTransport _transport;
        private readonly ShooterBattleDataPlaneOptions _options;
        private readonly ConcurrentQueue<(uint OpCode, ArraySegment<byte> Payload)> _pushQueue = new();
        private ShooterClientSession? _session;
        private ShooterClientBattleHandle? _battle;
        private long _lastReliableEventAckRequested;
        private int _queueDepth;
        private int _peakQueueDepth;
        private long _enqueuedPushCount;
        private long _processedPushCount;
        private long _drainCount;
        private long _budgetLimitedDrainCount;
        private int _lastDrainProcessedCount;
        private long _lastDrainElapsedTimestampTicks;
        private bool _disposed;

        public event Action<uint, ArraySegment<byte>, ShooterSnapshotApplyResult>? SnapshotPushDispatched;

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public ShooterSnapshotApplyResult LastPushResult { get; private set; } = ShooterSnapshotApplyResult.Ignored;

        public ShooterBattleDataPlaneDiagnostics Diagnostics => new ShooterBattleDataPlaneDiagnostics(
            Volatile.Read(ref _queueDepth),
            Volatile.Read(ref _peakQueueDepth),
            Interlocked.Read(ref _enqueuedPushCount),
            Interlocked.Read(ref _processedPushCount),
            Interlocked.Read(ref _drainCount),
            Interlocked.Read(ref _budgetLimitedDrainCount),
            Volatile.Read(ref _lastDrainProcessedCount),
            Volatile.Read(ref _lastDrainElapsedTimestampTicks) * 1000d / Stopwatch.Frequency);

        public ShooterBattleDataPlane(NetworkTransport transport)
            : this(transport, ShooterBattleDataPlaneOptions.Default)
        {
        }

        public ShooterBattleDataPlane(NetworkTransport transport, ShooterBattleDataPlaneOptions options)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _transport.RawServerPushReceived += OnServerPushReceived;
            _transport.ConnectionEstablished += OnConnectionEstablished;
        }

        public void AttachBattle(ShooterClientBattleHandle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _session = battle.Session;
            _lastReliableEventAckRequested = battle.Session.LastReliableEventAck;
        }

        /// <summary>
        /// Enqueues the push (fires on the transport's receive thread). ApplyGatewayPush is run on the
        /// main thread during <see cref="Drain"/>, so it can't race <c>session.Tick</c>. The request/response
        /// path (input submit) is unaffected — its response is matched inline by the RequestClient, so it
        /// doesn't depend on the main-thread pump (which would deadlock an awaited submit).
        /// </summary>
        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (_disposed)
            {
                return;
            }

            _pushQueue.Enqueue((opCode, payload));
            Interlocked.Increment(ref _enqueuedPushCount);
            UpdatePeakQueueDepth(Interlocked.Increment(ref _queueDepth));
        }

        /// <summary>Drains queued battle pushes on the caller's (main) thread. Call from the host tick loop.</summary>
        public int Drain()
        {
            var startedAt = Stopwatch.GetTimestamp();
            var maxElapsedTicks = Math.Max(1L, (long)(_options.MaxDrainDuration.TotalSeconds * Stopwatch.Frequency));
            var processed = 0;
            while (processed < _options.MaxPushesPerDrain &&
                   (processed == 0 || Stopwatch.GetTimestamp() - startedAt < maxElapsedTicks) &&
                   _pushQueue.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _queueDepth);
                ProcessPush(item.OpCode, item.Payload);
                processed++;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            Volatile.Write(ref _lastDrainProcessedCount, processed);
            Volatile.Write(ref _lastDrainElapsedTimestampTicks, elapsedTicks);
            Interlocked.Add(ref _processedPushCount, processed);
            Interlocked.Increment(ref _drainCount);
            if (Volatile.Read(ref _queueDepth) > 0 &&
                (processed >= _options.MaxPushesPerDrain || elapsedTicks >= maxElapsedTicks))
            {
                Interlocked.Increment(ref _budgetLimitedDrainCount);
            }

            return processed;
        }

        private void UpdatePeakQueueDepth(int depth)
        {
            var observed = Volatile.Read(ref _peakQueueDepth);
            while (depth > observed)
            {
                var previous = Interlocked.CompareExchange(ref _peakQueueDepth, depth, observed);
                if (previous == observed)
                {
                    return;
                }

                observed = previous;
            }
        }

        private void ProcessPush(uint opCode, ArraySegment<byte> payload)
        {
            if (_disposed)
            {
                return;
            }

            ServerPushReceived?.Invoke(opCode, payload);

            var session = _session;
            var result = session == null
                ? ShooterSnapshotApplyResult.Ignored
                : session.ApplyGatewayPush(opCode, payload);

            LastPushResult = result;
            SnapshotPushDispatched?.Invoke(opCode, payload, result);
            AcknowledgeReliableBattleEventsIfNeededAsync();
            RequestFullSnapshotResyncIfNeededAsync();
        }

        private void OnConnectionEstablished()
        {
            if (_disposed)
            {
                return;
            }

            // The engine re-authenticates (RenewSession→SubscribeStateSync) on reconnect, re-establishing the
            // push stream. Trigger a resync check in case the session flagged a gap during the outage.
            RequestFullSnapshotResyncIfNeededAsync();
        }

        private void AcknowledgeReliableBattleEventsIfNeededAsync()
        {
            var battle = _battle;
            if (battle == null
                || battle.Session.NeedsReliableEventResync
                || battle.Session.LastReliableEventAck <= _lastReliableEventAckRequested)
            {
                return;
            }

            _lastReliableEventAckRequested = battle.Session.LastReliableEventAck;
            _ = AcknowledgeReliableBattleEventsAsync(battle, _lastReliableEventAckRequested);
        }

        private async Task AcknowledgeReliableBattleEventsAsync(ShooterClientBattleHandle battle, long requestedSequence)
        {
            try
            {
                var result = await battle.AcknowledgeReliableBattleEventsAsync().ConfigureAwait(false);
                if (!result.Success)
                {
                    if (_lastReliableEventAckRequested == requestedSequence)
                    {
                        _lastReliableEventAckRequested = Math.Max(0L, result.AcceptedAckSequence);
                    }

                    await battle.RequestFullSnapshotBaselineAsync("ReliableEventGap").ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
                if (_lastReliableEventAckRequested == requestedSequence)
                {
                    _lastReliableEventAckRequested = Math.Max(0L, requestedSequence - 1L);
                }
            }
        }

        private void RequestFullSnapshotResyncIfNeededAsync()
        {
            var battle = _battle;
            if (battle == null)
            {
                return;
            }

            _ = RequestFullSnapshotResyncIfNeededAsync(battle);
        }

        private async Task RequestFullSnapshotResyncIfNeededAsync(ShooterClientBattleHandle battle)
        {
            try
            {
                await battle.RequestFullSnapshotResyncIfNeededAsync(AutomaticFullStateSyncTimeout).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _transport.RawServerPushReceived -= OnServerPushReceived;
            _transport.ConnectionEstablished -= OnConnectionEstablished;
            while (_pushQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queueDepth);
            }
        }
    }
}
