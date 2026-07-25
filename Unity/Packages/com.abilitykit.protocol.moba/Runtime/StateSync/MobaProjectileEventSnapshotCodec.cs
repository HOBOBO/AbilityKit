using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.StateSync
{
    public enum ProjectileEventKind : byte
    {
        Spawn = 1,
        Hit = 2,
        Exit = 3,
    }

    [MemoryPackable]
    public partial struct MobaProjectileEventSnapshotEntry
    {
        [MemoryPackOrder(0)] public int Kind;
        [MemoryPackOrder(1)] public int ProjectileActorId;
        [MemoryPackOrder(2)] public int OwnerActorId;
        [MemoryPackOrder(3)] public int TemplateId;
        [MemoryPackOrder(4)] public int LauncherActorId;
        [MemoryPackOrder(5)] public int RootActorId;
        [MemoryPackOrder(6)] public float X;
        [MemoryPackOrder(7)] public float Y;
        [MemoryPackOrder(8)] public float Z;
        [MemoryPackOrder(9)] public int HitCollider;
        [MemoryPackOrder(10)] public int ExitReason;
        [MemoryPackOrder(11)] public int ProjectileId;
        [MemoryPackOrder(12)] public float ForwardX;
        [MemoryPackOrder(13)] public float ForwardY;
        [MemoryPackOrder(14)] public float ForwardZ;

        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason)
            : this(kind, projectileActorId, ownerActorId, templateId, launcherActorId, rootActorId, x, y, z, hitCollider, exitReason, 0)
        {
        }

        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason, int projectileId)
            : this(kind, projectileActorId, ownerActorId, templateId, launcherActorId, rootActorId, x, y, z, hitCollider, exitReason, projectileId, 0f, 0f, 1f)
        {
        }

        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason, int projectileId, float forwardX, float forwardY, float forwardZ)
        {
            Kind = kind;
            ProjectileActorId = projectileActorId;
            OwnerActorId = ownerActorId;
            TemplateId = templateId;
            LauncherActorId = launcherActorId;
            RootActorId = rootActorId;
            X = x;
            Y = y;
            Z = z;
            HitCollider = hitCollider;
            ExitReason = exitReason;
            ProjectileId = projectileId;
            ForwardX = forwardX;
            ForwardY = forwardY;
            ForwardZ = forwardZ;
        }
    }

    [MemoryPackable]
    public partial struct MobaProjectileEventSnapshotPayload
    {
        [MemoryPackOrder(0)] public MobaProjectileEventSnapshotEntry[] Entries;

        [MemoryPackConstructor]
        public MobaProjectileEventSnapshotPayload(MobaProjectileEventSnapshotEntry[] entries)
        {
            Entries = entries;
        }
    }

    public static class MobaProjectileEventSnapshotCodec
    {
        public static byte[] Serialize(MobaProjectileEventSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaProjectileEventSnapshotEntry>();
            var payload = new MobaProjectileEventSnapshotPayload { Entries = entries };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaProjectileEventSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaProjectileEventSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaProjectileEventSnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<MobaProjectileEventSnapshotEntry>();
        }
    }
}
