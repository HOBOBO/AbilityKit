using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaActorTransformSnapshotService : IService
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaActorRegistry _registry;
        private FrameIndex _lastFrame;

        public MobaActorTransformSnapshotService(MobaLobbyStateService lobby, MobaActorRegistry registry)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            // Only after game started.
            if (!_lobby.Started)
            {
                snapshot = default;
                return false;
            }

            // At most once per frame.
            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            // For now: snapshot all known actors in registry.
            // If you later remove registry, we can iterate group by ActorMatcher.Transform.
            var entries = BuildEntries();
            if (entries.Length == 0)
            {
                snapshot = default;
                return false;
            }

            var payload = MobaActorTransformSnapshotCodec.Serialize(entries);
            snapshot = new WorldStateSnapshot((int)MobaOpCode.ActorTransformSnapshot, payload);
            return true;
        }

        private (int actorId, float x, float y, float z)[] BuildEntries()
        {
            var tmp = new System.Collections.Generic.List<(int, float, float, float)>(8);

            foreach (var kv in _registry.Entries)
            {
                var id = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                var p = e.transform.Value.Position;
                tmp.Add((id, p.X, p.Y, p.Z));
            }

            return tmp.ToArray();
        }

        public void Dispose()
        {
        }
    }
}
