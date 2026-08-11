using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the transport bindings and mutable resources for one remote replication generation.
    /// BattleSessionFeature remains the business callback facade during the staged migration.
    /// </summary>
    internal sealed class BattleReplicationRuntime : IDisposable
    {
        private int _generation;
        private Action<object> _snapshotPushed;
        private Action<object> _reliableEventsPushed;
        private Action _connectionClosed;
        private Action _connectionEstablished;
        private Action<Exception> _authenticationFailed;
        private Func<string> _getReliableEventEpoch;
        private Func<long> _getReliableEventLastAcknowledgedSequence;
        private Action<int> _submitInputAck;
        private Func<string> _previousGetReliableEventEpoch;
        private Func<long> _previousGetReliableEventLastAcknowledgedSequence;
        private Action<int> _previousSubmitInputAck;

        internal NetworkTransport Transport { get; private set; }
        internal MobaClientAuthoritativeInterpolationSyncController InterpolationController { get; private set; }
        internal MobaClientReplicationPipeline ReplicationPipeline { get; private set; }
        internal MobaSynchronizationHealthEvaluator SynchronizationHealthEvaluator { get; private set; }
        internal MobaSynchronizationHealthSnapshot SynchronizationHealth { get; set; }
        internal SyncHealthReport SynchronizationHealthReport { get; set; } = SyncHealthReport.Empty;
        internal float SynchronizationHealthSampleElapsed { get; set; }
        internal MobaSnapshotAdmission SnapshotAdmission { get; private set; }
        internal MobaAuthoritativeSnapshotState AuthoritativeSnapshotState { get; private set; }
        internal MobaReliableBattleEventCursor ReliableEventCursor { get; private set; }
        internal Queue<WireReliableBattleEventPush> PendingReliableEventBatches { get; } =
            new Queue<WireReliableBattleEventPush>();
        internal int LastServerAckFrame { get; set; }
        internal bool PendingStateImport { get; set; }
        internal bool IsBuilt => Transport != null;

        internal bool Build(
            NetworkTransport transport,
            int tickRate,
            ulong roomId,
            string battleId,
            in MobaReliableBattleEventCheckpoint reliableEventCheckpoint,
            Action<object> onSnapshotPushed,
            Action<object> onReliableEventsPushed,
            Action onConnectionClosed,
            Action onConnectionEstablished,
            Action<Exception> onAuthenticationFailed = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (onSnapshotPushed == null) throw new ArgumentNullException(nameof(onSnapshotPushed));
            if (onReliableEventsPushed == null) throw new ArgumentNullException(nameof(onReliableEventsPushed));
            if (onConnectionClosed == null) throw new ArgumentNullException(nameof(onConnectionClosed));
            if (onConnectionEstablished == null) throw new ArgumentNullException(nameof(onConnectionEstablished));

            Dispose();
            var generation = ++_generation;
            var checkpointAccepted = true;
            try
            {
                Transport = transport;
                InterpolationController = new MobaClientAuthoritativeInterpolationSyncController(
                    MobaRemoteInterpolationPlayback.CreateFrameTimelineConfig(tickRate));
                ReplicationPipeline = new MobaClientReplicationPipeline(InterpolationController);
                SynchronizationHealthEvaluator = new MobaSynchronizationHealthEvaluator();
                SnapshotAdmission = new MobaSnapshotAdmission();
                SnapshotAdmission.Reset(roomId);
                AuthoritativeSnapshotState = new MobaAuthoritativeSnapshotState();
                ReliableEventCursor = new MobaReliableBattleEventCursor(battleId ?? string.Empty);
                checkpointAccepted = !reliableEventCheckpoint.IsValid ||
                    ReliableEventCursor.TryRestore(in reliableEventCheckpoint);
                PendingStateImport = true;

                _snapshotPushed = value =>
                {
                    if (IsCurrent(generation, transport)) onSnapshotPushed(value);
                };
                _reliableEventsPushed = value =>
                {
                    if (IsCurrent(generation, transport)) onReliableEventsPushed(value);
                };
                _connectionClosed = () =>
                {
                    if (IsCurrent(generation, transport)) onConnectionClosed();
                };
                _connectionEstablished = () =>
                {
                    if (IsCurrent(generation, transport)) onConnectionEstablished();
                };
                _authenticationFailed = ex =>
                {
                    if (IsCurrent(generation, transport)) onAuthenticationFailed?.Invoke(ex);
                };
                _getReliableEventEpoch = () =>
                    IsCurrent(generation, transport)
                        ? ReliableEventCursor?.Epoch ?? string.Empty
                        : string.Empty;
                _getReliableEventLastAcknowledgedSequence = () =>
                    IsCurrent(generation, transport)
                        ? ReliableEventCursor?.LastAcknowledgedSequence ?? 0L
                        : 0L;
                _submitInputAck = serverFrame =>
                {
                    if (!IsCurrent(generation, transport)) return;
                    LastServerAckFrame = serverFrame;
                    ReplicationPipeline?.AcknowledgeInput(serverFrame);
                };

                var options = transport.Options;
                _previousGetReliableEventEpoch = options.GetReliableEventEpoch;
                _previousGetReliableEventLastAcknowledgedSequence =
                    options.GetReliableEventLastAcknowledgedSequence;
                _previousSubmitInputAck = options.OnSubmitInputAck;
                options.GetReliableEventEpoch = _getReliableEventEpoch;
                options.GetReliableEventLastAcknowledgedSequence =
                    _getReliableEventLastAcknowledgedSequence;
                options.OnSubmitInputAck = _submitInputAck;

                transport.StateSyncSnapshotPushed += _snapshotPushed;
                transport.ReliableEventsPushed += _reliableEventsPushed;
                transport.ConnectionClosed += _connectionClosed;
                transport.ConnectionEstablished += _connectionEstablished;
                transport.AuthenticationFailed += _authenticationFailed;
                return checkpointAccepted;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _generation++;
            var transport = Transport;
            Transport = null;
            if (transport != null)
            {
                if (_snapshotPushed != null)
                    transport.StateSyncSnapshotPushed -= _snapshotPushed;
                if (_reliableEventsPushed != null)
                    transport.ReliableEventsPushed -= _reliableEventsPushed;
                if (_connectionClosed != null)
                    transport.ConnectionClosed -= _connectionClosed;
                if (_connectionEstablished != null)
                    transport.ConnectionEstablished -= _connectionEstablished;
                if (_authenticationFailed != null)
                    transport.AuthenticationFailed -= _authenticationFailed;

                var options = transport.Options;
                if (ReferenceEquals(options.GetReliableEventEpoch, _getReliableEventEpoch))
                    options.GetReliableEventEpoch = _previousGetReliableEventEpoch;
                if (ReferenceEquals(
                        options.GetReliableEventLastAcknowledgedSequence,
                        _getReliableEventLastAcknowledgedSequence))
                {
                    options.GetReliableEventLastAcknowledgedSequence =
                        _previousGetReliableEventLastAcknowledgedSequence;
                }
                if (ReferenceEquals(options.OnSubmitInputAck, _submitInputAck))
                    options.OnSubmitInputAck = _previousSubmitInputAck;
            }

            InterpolationController?.Reset();
            ReplicationPipeline?.ResetDiagnostics();
            SynchronizationHealthEvaluator?.Reset();
            AuthoritativeSnapshotState?.Reset();
            ReliableEventCursor?.Reset();
            PendingReliableEventBatches.Clear();

            InterpolationController = null;
            ReplicationPipeline = null;
            SynchronizationHealthEvaluator = null;
            SynchronizationHealth = default;
            SynchronizationHealthReport = SyncHealthReport.Empty;
            SynchronizationHealthSampleElapsed = 0f;
            SnapshotAdmission = null;
            AuthoritativeSnapshotState = null;
            ReliableEventCursor = null;
            LastServerAckFrame = 0;
            PendingStateImport = false;
            _snapshotPushed = null;
            _reliableEventsPushed = null;
            _connectionClosed = null;
            _connectionEstablished = null;
            _authenticationFailed = null;
            _getReliableEventEpoch = null;
            _getReliableEventLastAcknowledgedSequence = null;
            _submitInputAck = null;
            _previousGetReliableEventEpoch = null;
            _previousGetReliableEventLastAcknowledgedSequence = null;
            _previousSubmitInputAck = null;
        }

        private bool IsCurrent(int generation, NetworkTransport transport)
        {
            return generation == _generation && ReferenceEquals(Transport, transport);
        }
    }
}
