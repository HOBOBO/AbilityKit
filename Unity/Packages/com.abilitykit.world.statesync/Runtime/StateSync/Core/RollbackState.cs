using System;
using System.IO;

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
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(EntityId);
            writer.Write(position.X);
            writer.Write(position.Y);
            writer.Write(position.Z);
            writer.Write(rotation.X);
            writer.Write(rotation.Y);
            writer.Write(rotation.Z);
            writer.Write(rotation.W);
            writer.Write(velocity.X);
            writer.Write(velocity.Y);
            writer.Write(velocity.Z);
            writer.Write(healthPercent);
            writer.Write(StateFlags);
            writer.Write(ActiveAbilityMask);
            writer.Write(TeamId);
            writer.Write(ControlFlags);

            return stream.ToArray();
        }

        public void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            EntityId = reader.ReadInt64();
            position = new Snapshot.Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            rotation = new Snapshot.Quat(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            velocity = new Snapshot.Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            healthPercent = reader.ReadByte();
            StateFlags = reader.ReadUInt32();
            ActiveAbilityMask = reader.ReadInt64();
            TeamId = reader.ReadInt32();
            ControlFlags = reader.ReadByte();
        }
    }
}
