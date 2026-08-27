#nullable enable

using System;
using System.IO;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    public static class ShooterPackedSnapshotFlags
    {
        public const uint Full = 1u << 0;
        public const uint Delta = 1u << 1;
        public const uint KeyFrame = 1u << 2;
        public const uint AuthorityOverride = 1u << 3;
    }

    public static class ShooterPackedEntityFlags
    {
        public const byte Alive = 1 << 0;
        public const byte Player = 1 << 1;
        public const byte Projectile = 1 << 2;
        public const byte Enemy = 1 << 3;
        public const byte Spawned = 1 << 4;
        public const byte Despawned = 1 << 5;
        public const byte DirtyTransform = 1 << 6;
        public const byte DirtyStats = 1 << 7;
    }

    public static class ShooterPackedEntityKinds
    {
        public const int Player = 1;
        public const int Projectile = 2;
        public const int Enemy = 3;
    }

    public static class ShooterPackedComponentKinds
    {
        public const int EntityLifecycle = 1;
        public const int Transform = 2;
        public const int Health = 3;
        public const int Score = 4;
        public const int ProjectileLifetime = 5;
        public const int RuntimeMetadata = 6;
    }

    public partial struct ShooterPackedComponentChunk
    {
        [MemoryPackConstructor]
        public ShooterPackedComponentChunk(
            int componentKind,
            int entityKind,
            int count,
            int[] entityIds,
            float[] valueX,
            float[] valueY,
            float[] valueZ,
            float[] valueW,
            int[] intValues,
            byte[] flags,
            int[] ownerIds,
            int[] aux)
        {
            ComponentKind = componentKind;
            EntityKind = entityKind;
            Count = count;
            EntityIds = entityIds;
            ValueX = valueX;
            ValueY = valueY;
            ValueZ = valueZ;
            ValueW = valueW;
            IntValues = intValues;
            Flags = flags;
            OwnerIds = ownerIds;
            Aux = aux;
        }

        public static ShooterPackedComponentChunk Empty(int componentKind, int entityKind)
        {
            return new ShooterPackedComponentChunk(
                componentKind,
                entityKind,
                0,
                Array.Empty<int>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<float>(),
                Array.Empty<int>(),
                Array.Empty<byte>(),
                Array.Empty<int>(),
                Array.Empty<int>());
        }
    }

    public partial struct ShooterCommandAcknowledgement
    {
        public ShooterCommandAcknowledgement(int playerId, ulong commandSequence)
        {
            PlayerId = playerId;
            CommandSequence = commandSequence;
        }
    }

    public partial struct ShooterPackedSnapshotPayload
    {
        [MemoryPackConstructor]
        public ShooterPackedSnapshotPayload(
            int version,
            ulong worldId,
            int frame,
            long serverTick,
            uint snapshotFlags,
            uint stateHash,
            int entityCount,
            byte[] extensionPayload,
            ShooterPackedComponentChunk[] componentChunks,
            ShooterCommandAcknowledgement[]? acknowledgedCommands = null)
        {
            Version = version;
            WorldId = worldId;
            Frame = frame;
            ServerTick = serverTick;
            SnapshotFlags = snapshotFlags;
            StateHash = stateHash;
            EntityCount = entityCount;
            ExtensionPayload = extensionPayload;
            ComponentChunks = componentChunks;
            AcknowledgedCommands = acknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>();
        }

        public static ShooterPackedSnapshotPayload Empty(int frame = 0)
        {
            return new ShooterPackedSnapshotPayload(
                ShooterPackedSnapshotCodec.CurrentVersion,
                0,
                frame,
                0,
                ShooterPackedSnapshotFlags.Full,
                0,
                0,
                Array.Empty<byte>(),
                Array.Empty<ShooterPackedComponentChunk>());
        }
    }

    [MemoryPackable]
    internal partial struct ShooterLegacyPackedSnapshotPayload
    {
        [MemoryPackOrder(0)] public int Version;
        [MemoryPackOrder(1)] public ulong WorldId;
        [MemoryPackOrder(2)] public int Frame;
        [MemoryPackOrder(3)] public long ServerTick;
        [MemoryPackOrder(4)] public uint SnapshotFlags;
        [MemoryPackOrder(5)] public uint StateHash;
        [MemoryPackOrder(6)] public int EntityCount;
        [MemoryPackOrder(7)] public byte[] ExtensionPayload;
        [MemoryPackOrder(8)] public ShooterPackedComponentChunk[] ComponentChunks;
    }

    public static class ShooterPackedSnapshotCodec
    {
        public const int CurrentVersion = 3;

        public static byte[] Serialize(in ShooterPackedSnapshotPayload snapshot)
        {
            return MemoryPackSerializer.Serialize(snapshot);
        }

        public static ShooterPackedSnapshotPayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return ShooterPackedSnapshotPayload.Empty();
            }

            try
            {
                var value = MemoryPackSerializer.Deserialize<ShooterPackedSnapshotPayload>(payload);
                return new ShooterPackedSnapshotPayload(
                    value.Version,
                    value.WorldId,
                    value.Frame,
                    value.ServerTick,
                    value.SnapshotFlags,
                    value.StateHash,
                    value.EntityCount,
                    value.ExtensionPayload ?? Array.Empty<byte>(),
                    value.ComponentChunks ?? Array.Empty<ShooterPackedComponentChunk>(),
                    value.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }
            catch (EndOfStreamException)
            {
                var legacy = MemoryPackSerializer.Deserialize<ShooterLegacyPackedSnapshotPayload>(payload);
                return new ShooterPackedSnapshotPayload(
                    legacy.Version,
                    legacy.WorldId,
                    legacy.Frame,
                    legacy.ServerTick,
                    legacy.SnapshotFlags,
                    legacy.StateHash,
                    legacy.EntityCount,
                    legacy.ExtensionPayload ?? Array.Empty<byte>(),
                    legacy.ComponentChunks ?? Array.Empty<ShooterPackedComponentChunk>());
            }
        }
    }
}
