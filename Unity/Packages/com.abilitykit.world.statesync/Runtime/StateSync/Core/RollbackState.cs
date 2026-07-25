using System;
using MemoryPack;

namespace AbilityKit.Ability.StateSync
{
    public sealed class RollbackState : IRollbackState
    {
        public int SnapshotKey { get; }
        public byte[] Data { get; private set; }

        public RollbackState(int snapshotKey)
        {
            SnapshotKey = snapshotKey;
        }

        public RollbackState(int snapshotKey, byte[] data)
        {
            SnapshotKey = snapshotKey;
            Data = data;
        }

        public byte[] Serialize()
        {
            return Data ?? Array.Empty<byte>();
        }

        public void Deserialize(byte[] data)
        {
            Data = data ?? Array.Empty<byte>();
        }
    }

    public sealed class EntityRollbackState : IRollbackState
    {
        public int SnapshotKey => _snapshotKey;
        private readonly int _snapshotKey;

        public long EntityId;
        public Snapshot.Vec3 position;
        public Snapshot.Quat rotation;
        public Snapshot.Vec3 velocity;
        public byte healthPercent;
        public uint StateFlags;
        public long ActiveAbilityMask;
        public int TeamId;
        public byte ControlFlags;

        public EntityRollbackState(long entityId)
        {
            _snapshotKey = entityId.GetHashCode();
            EntityId = entityId;
        }

        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(new EntityRollbackStatePayload(
                EntityId, position, rotation, velocity,
                healthPercent, StateFlags, ActiveAbilityMask, TeamId, ControlFlags));
        }

        public void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<EntityRollbackStatePayload>(data);
            EntityId = p.EntityId;
            position = p.Position;
            rotation = p.Rotation;
            velocity = p.Velocity;
            healthPercent = p.HealthPercent;
            StateFlags = p.StateFlags;
            ActiveAbilityMask = p.ActiveAbilityMask;
            TeamId = p.TeamId;
            ControlFlags = p.ControlFlags;
        }
    }

    [MemoryPackable]
    public readonly partial struct EntityRollbackStatePayload
    {
        [MemoryPackOrder(0)] public readonly long EntityId;
        [MemoryPackOrder(1)] public readonly Snapshot.Vec3 Position;
        [MemoryPackOrder(2)] public readonly Snapshot.Quat Rotation;
        [MemoryPackOrder(3)] public readonly Snapshot.Vec3 Velocity;
        [MemoryPackOrder(4)] public readonly byte HealthPercent;
        [MemoryPackOrder(5)] public readonly uint StateFlags;
        [MemoryPackOrder(6)] public readonly long ActiveAbilityMask;
        [MemoryPackOrder(7)] public readonly int TeamId;
        [MemoryPackOrder(8)] public readonly byte ControlFlags;

                public EntityRollbackStatePayload(
            long entityId,
            Snapshot.Vec3 position,
            Snapshot.Quat rotation,
            Snapshot.Vec3 velocity,
            byte healthPercent,
            uint stateFlags,
            long activeAbilityMask,
            int teamId,
            byte controlFlags)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            HealthPercent = healthPercent;
            StateFlags = stateFlags;
            ActiveAbilityMask = activeAbilityMask;
            TeamId = teamId;
            ControlFlags = controlFlags;
        }
    }
}
