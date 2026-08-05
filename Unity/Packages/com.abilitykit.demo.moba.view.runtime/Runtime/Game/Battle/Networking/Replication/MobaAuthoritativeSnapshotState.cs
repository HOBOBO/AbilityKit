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

        public int ActorCount => _actors.Count;

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
                Array.Empty<int>());
        }

        public void Reset()
        {
            _actors.Clear();
            _orderedActorIds.Clear();
        }
    }
}
