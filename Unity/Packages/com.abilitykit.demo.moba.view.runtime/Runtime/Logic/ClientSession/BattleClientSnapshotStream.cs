using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleClientSnapshotStream
    {
        private readonly object _gate = new object();
        private readonly int _maxPendingSnapshots;
        private readonly Queue<GatewayStateSyncSnapshot> _pending =
            new Queue<GatewayStateSyncSnapshot>();
        private readonly Dictionary<string, IBattleClientProjection> _projections =
            new Dictionary<string, IBattleClientProjection>(StringComparer.Ordinal);
        private readonly Dictionary<int, GatewayStateSyncActorSnapshot> _actors =
            new Dictionary<int, GatewayStateSyncActorSnapshot>();
        private GatewayStateSyncSnapshot _lastSnapshot;
        private bool _hasSnapshot;
        private bool _requiresFullSnapshot = true;
        private bool _recoveryPending;
        private long _droppedSnapshots;

        internal BattleClientSnapshotStream(int maxPendingSnapshots)
        {
            _maxPendingSnapshots = maxPendingSnapshots;
        }

        internal ulong WorldId { get; private set; }
        internal int LastSnapshotFrame => _hasSnapshot ? _lastSnapshot.Frame : 0;
        internal int ProjectionCount => _projections.Count;
        internal bool RequiresFullSnapshot => _requiresFullSnapshot;

        internal int PendingSnapshotCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        internal long DiscardedPendingSnapshotCount
        {
            get { lock (_gate) return _droppedSnapshots; }
        }

        internal void Queue(in GatewayStateSyncSnapshot snapshot)
        {
            lock (_gate)
            {
                if (_pending.Count == _maxPendingSnapshots)
                {
                    _droppedSnapshots += _pending.Count;
                    _pending.Clear();
                    _recoveryPending = true;
                    if (!snapshot.IsFullSnapshot)
                    {
                        _droppedSnapshots++;
                        return;
                    }
                }

                if (_recoveryPending && !snapshot.IsFullSnapshot)
                {
                    _droppedSnapshots++;
                    return;
                }
                _pending.Enqueue(snapshot);
            }
        }

        internal void NotifyConnectionLost()
        {
            lock (_gate)
            {
                _droppedSnapshots += _pending.Count;
                _pending.Clear();
                _recoveryPending = true;
            }
        }

        internal bool ConsumeRecoverySignal()
        {
            lock (_gate)
            {
                if (!_recoveryPending) return false;
                _recoveryPending = false;
            }
            _requiresFullSnapshot = true;
            return true;
        }

        internal bool Drain()
        {
            var recoverySignaled = false;
            int batchSize;
            lock (_gate) batchSize = _pending.Count;
            var processed = 0;
            while (true)
            {
                GatewayStateSyncSnapshot snapshot;
                bool hasSnapshot;
                bool recoverNow;
                lock (_gate)
                {
                    recoverNow = _recoveryPending;
                    _recoveryPending = false;
                    hasSnapshot = processed < batchSize && _pending.Count != 0;
                    snapshot = hasSnapshot ? _pending.Dequeue() : default;
                }
                if (recoverNow)
                {
                    _requiresFullSnapshot = true;
                    recoverySignaled = true;
                }
                if (!hasSnapshot) return recoverySignaled;
                Present(in snapshot, allowSameFrame: false);
                processed++;
            }
        }

        internal bool Present(in GatewayStateSyncSnapshot snapshot, bool allowSameFrame)
        {
            if (snapshot.WorldId == 0 || snapshot.Frame < 0 ||
                (WorldId != 0 && snapshot.WorldId != WorldId)) return false;
            if (_requiresFullSnapshot && !snapshot.IsFullSnapshot) return false;
            if (_hasSnapshot && !_requiresFullSnapshot)
            {
                if (!string.Equals(_lastSnapshot.EventEpoch, snapshot.EventEpoch,
                        StringComparison.Ordinal) && !snapshot.IsFullSnapshot) return false;
                if ((snapshot.Frame < _lastSnapshot.Frame ||
                     (!allowSameFrame && snapshot.Frame == _lastSnapshot.Frame)) &&
                    string.Equals(_lastSnapshot.EventEpoch, snapshot.EventEpoch,
                        StringComparison.Ordinal)) return false;
            }

            if (snapshot.IsFullSnapshot) _actors.Clear();
            var actors = snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
            foreach (var actor in actors)
                if (actor.ActorId > 0) _actors[actor.ActorId] = actor;
            if (!snapshot.IsFullSnapshot && snapshot.RemovedActorIds != null)
                foreach (var actorId in snapshot.RemovedActorIds) _actors.Remove(actorId);

            WorldId = snapshot.WorldId;
            _lastSnapshot = snapshot;
            _hasSnapshot = true;
            _requiresFullSnapshot = false;
            foreach (var projection in _projections.Values) projection.Apply(in snapshot);
            return true;
        }

        internal void AttachProjection(string id, IBattleClientProjection projection)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Projection ID is required.", nameof(id));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (_projections.ContainsKey(id))
                throw new InvalidOperationException("Projection ID is already attached: " + id);
            projection.Reset();
            if (_hasSnapshot && !_requiresFullSnapshot)
                projection.Apply(CreateCurrentSnapshot());
            _projections.Add(id, projection);
        }

        internal bool DetachProjection(string id)
        {
            if (id == null || !_projections.TryGetValue(id, out var projection)) return false;
            _projections.Remove(id);
            projection.Reset();
            return true;
        }

        internal void Reset()
        {
            lock (_gate)
            {
                _pending.Clear();
                _recoveryPending = false;
            }
            _actors.Clear();
            _lastSnapshot = default;
            _hasSnapshot = false;
            _requiresFullSnapshot = true;
            WorldId = 0;
        }

        internal void ResetProjections()
        {
            foreach (var projection in _projections.Values) projection.Reset();
        }

        private GatewayStateSyncSnapshot CreateCurrentSnapshot()
        {
            var ids = new List<int>(_actors.Keys);
            ids.Sort();
            var actors = new GatewayStateSyncActorSnapshot[ids.Count];
            for (var i = 0; i < ids.Count; i++) actors[i] = _actors[ids[i]];
            return new GatewayStateSyncSnapshot(
                WorldId, _lastSnapshot.Frame, _lastSnapshot.Timestamp, true, actors,
                _lastSnapshot.SchemaVersion, eventWatermark: _lastSnapshot.EventWatermark,
                eventEpoch: _lastSnapshot.EventEpoch, serverTicks: _lastSnapshot.ServerTicks);
        }
    }
}
