using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaStateHashSnapshotService : IService
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaActorRegistry _registry;

        private FrameIndex _lastFrame;

        public int IntervalFrames { get; set; } = 10;

        public MobaStateHashSnapshotService(MobaLobbyStateService lobby, MobaActorRegistry registry)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_lobby.Started)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }

            var interval = IntervalFrames;
            if (interval <= 0) interval = 10;

            if ((frame.Value % interval) != 0)
            {
                snapshot = default;
                return false;
            }

            _lastFrame = frame;

            var hash = ComputeStateHash();
            var payload = MobaStateHashSnapshotCodec.Serialize(frame.Value, hash);
            snapshot = new WorldStateSnapshot((int)MobaOpCode.StateHashSnapshot, payload);
            return true;
        }

        private uint ComputeStateHash()
        {
            // Deterministic FNV-1a 32-bit over:
            // started flag + (actorId, x, y, z) entries sorted by actorId.
            var entries = new List<(int actorId, float x, float y, float z)>(16);
            foreach (var kv in _registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                var p = e.transform.Value.Position;
                entries.Add((actorId, p.X, p.Y, p.Z));
            }

            entries.Sort((a, b) => a.actorId.CompareTo(b.actorId));

            uint h = 2166136261u;

            AddByte(ref h, _lobby.Started ? (byte)1 : (byte)0);
            AddInt(ref h, entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var it = entries[i];
                AddInt(ref h, it.actorId);
                AddFloat(ref h, it.x);
                AddFloat(ref h, it.y);
                AddFloat(ref h, it.z);
            }

            return h;
        }

        private static void AddByte(ref uint h, byte v)
        {
            h ^= v;
            h *= 16777619u;
        }

        private static void AddInt(ref uint h, int v)
        {
            unchecked
            {
                AddUInt(ref h, (uint)v);
            }
        }

        private static void AddUInt(ref uint h, uint v)
        {
            AddByte(ref h, (byte)(v & 0xFF));
            AddByte(ref h, (byte)((v >> 8) & 0xFF));
            AddByte(ref h, (byte)((v >> 16) & 0xFF));
            AddByte(ref h, (byte)((v >> 24) & 0xFF));
        }

        private static void AddFloat(ref uint h, float v)
        {
            var bits = BitConverter.SingleToInt32Bits(v);
            AddInt(ref h, bits);
        }

        public void Dispose()
        {
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
