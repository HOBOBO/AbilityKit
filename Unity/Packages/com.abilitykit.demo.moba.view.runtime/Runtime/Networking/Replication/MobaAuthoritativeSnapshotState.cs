#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// Materializes admitted full/delta snapshots into a complete authoritative actor view.
    /// </summary>
    public sealed class MobaAuthoritativeSnapshotState
    {
        private readonly Dictionary<int, GatewayStateSyncActorSnapshot> _actors =
            new Dictionary<int, GatewayStateSyncActorSnapshot>();
        private readonly List<int> _orderedActorIds = new List<int>();
        private readonly Dictionary<int, GatewayStateSyncActorSnapshot> _fixedBaseline = new();
        private readonly HashSet<Guid> _retiredGenerations = new();
        private Guid _generation;
        private ulong _worldId;
        private long _baseSnapshotId;
        private long _lastSnapshotId;
        private int _lastFrame;

        public int ActorCount => _actors.Count;

        internal bool HasFixedStream => _generation != Guid.Empty;

        internal MobaSnapshotAdmissionStatus PrepareFixed(in GatewayStateSyncSnapshot snapshot,
            out GatewayStateSyncSnapshot materialized)
        {
            materialized = default;
            var generation = snapshot.StateStreamGeneration;
            if (generation == Guid.Empty || snapshot.SnapshotId <= 0 || snapshot.BaseSnapshotId < 0 ||
                snapshot.BaseSnapshotId >= snapshot.SnapshotId || snapshot.IsFullSnapshot != (snapshot.BaseSnapshotId == 0))
                return MobaSnapshotAdmissionStatus.BaselineRequired;

            if (_retiredGenerations.Contains(generation) || snapshot.Frame < _lastFrame ||
                (generation == _generation && snapshot.SnapshotId <= _lastSnapshotId))
                return MobaSnapshotAdmissionStatus.StaleOrDuplicate;

            if (!snapshot.IsFullSnapshot &&
                (generation != _generation || snapshot.WorldId != _worldId || snapshot.BaseSnapshotId != _baseSnapshotId))
                return MobaSnapshotAdmissionStatus.BaselineRequired;

            var target = snapshot.IsFullSnapshot
                ? new Dictionary<int, GatewayStateSyncActorSnapshot>()
                : new Dictionary<int, GatewayStateSyncActorSnapshot>(_fixedBaseline);
            var touched = new HashSet<int>();
            foreach (var actor in snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>())
            {
                if (actor.ActorId <= 0 || !touched.Add(actor.ActorId))
                    return MobaSnapshotAdmissionStatus.BaselineRequired;
                target[actor.ActorId] = actor;
            }
            foreach (var actorId in snapshot.RemovedActorIds)
                if (!touched.Add(actorId) || !target.Remove(actorId))
                    return MobaSnapshotAdmissionStatus.BaselineRequired;

            var actorIds = new List<int>(target.Keys);
            actorIds.Sort();
            var actors = new GatewayStateSyncActorSnapshot[actorIds.Count];
            for (int index = 0; index < actors.Length; index++) actors[index] = target[actorIds[index]];
            materialized = new GatewayStateSyncSnapshot(snapshot.WorldId, snapshot.Frame, snapshot.Timestamp,
                isFullSnapshot: true, actors, snapshot.SchemaVersion, eventWatermark: snapshot.EventWatermark,
                eventEpoch: snapshot.EventEpoch, payloadOpCode: snapshot.PayloadOpCode, payload: snapshot.Payload,
                serverTicks: snapshot.ServerTicks);
            return MobaSnapshotAdmissionStatus.Accepted;
        }

        internal void CommitFixed(in GatewayStateSyncSnapshot snapshot, in GatewayStateSyncSnapshot materialized)
        {
            if (snapshot.IsFullSnapshot)
            {
                if (_generation != Guid.Empty && _generation != snapshot.StateStreamGeneration)
                    _retiredGenerations.Add(_generation);
                _generation = snapshot.StateStreamGeneration;
                _worldId = snapshot.WorldId;
                _baseSnapshotId = snapshot.SnapshotId;
                _fixedBaseline.Clear();
                foreach (var actor in materialized.Actors) _fixedBaseline.Add(actor.ActorId, actor);
            }
            _lastSnapshotId = snapshot.SnapshotId;
            _lastFrame = snapshot.Frame;
            _actors.Clear();
            foreach (var actor in materialized.Actors) _actors.Add(actor.ActorId, actor);
        }

        public GatewayStateSyncSnapshot Apply(in GatewayStateSyncSnapshot snapshot)
        {
            if (snapshot.IsFullSnapshot)
            {
                _actors.Clear();
            }

            var actors = snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor.ActorId > 0)
                {
                    _actors[actor.ActorId] = actor;
                }
            }

            var removedActorIds = snapshot.RemovedActorIds ?? Array.Empty<int>();
            for (int i = 0; i < removedActorIds.Length; i++)
            {
                _actors.Remove(removedActorIds[i]);
            }

            _orderedActorIds.Clear();
            foreach (var actorId in _actors.Keys)
            {
                _orderedActorIds.Add(actorId);
            }
            _orderedActorIds.Sort();

            var materializedActors = new GatewayStateSyncActorSnapshot[_orderedActorIds.Count];
            for (int i = 0; i < _orderedActorIds.Count; i++)
            {
                materializedActors[i] = _actors[_orderedActorIds[i]];
            }

            return new GatewayStateSyncSnapshot(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.Timestamp,
                isFullSnapshot: true,
                materializedActors,
                snapshot.SchemaVersion,
                Array.Empty<int>(),
                snapshot.EventWatermark,
                snapshot.EventEpoch,
                snapshot.PayloadOpCode,
                snapshot.Payload,
                snapshot.ServerTicks);
        }

        internal void ResetForRecovery()
        {
            _actors.Clear();
            _orderedActorIds.Clear();
            _fixedBaseline.Clear();
            _baseSnapshotId = 0;
        }

        public void Reset()
        {
            ResetForRecovery();
            _retiredGenerations.Clear();
            _generation = Guid.Empty;
            _worldId = 0;
            _lastSnapshotId = 0;
            _lastFrame = 0;
        }
    }
}
