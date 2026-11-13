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
        Phase = 4,
    }

    public partial struct MobaProjectileEventSnapshotEntry
    {
        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason)
            : this(kind, projectileActorId, ownerActorId, templateId, launcherActorId, rootActorId, x, y, z, hitCollider, exitReason, 0)
        {
        }

        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason, int projectileId)
            : this(kind, projectileActorId, ownerActorId, templateId, launcherActorId, rootActorId, x, y, z, hitCollider, exitReason, projectileId, 0f, 0f, 1f)
        {
        }

        [MemoryPackConstructor]
        public MobaProjectileEventSnapshotEntry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason, int projectileId, float forwardX, float forwardY, float forwardZ)
            : this()
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

    public partial struct MobaProjectileEventSnapshotPayload
    {
        [MemoryPackConstructor]
        public MobaProjectileEventSnapshotPayload(MobaProjectileEventSnapshotEntry[] entries)
        {
            Entries = entries;
            Version = MobaProjectileEventSnapshotCodec.CurrentVersion;
        }
    }

    public static class MobaProjectileEventSnapshotCodec
    {
        public const int CurrentVersion = 2;
        public static byte[] Serialize(MobaProjectileEventSnapshotEntry[] entries)
        {
            entries ??= Array.Empty<MobaProjectileEventSnapshotEntry>();
            var payload = new MobaProjectileEventSnapshotPayload { Entries = entries, Version = CurrentVersion };
            return MemoryPackSerializer.Serialize(payload);
        }

        public static MobaProjectileEventSnapshotEntry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaProjectileEventSnapshotEntry>();

            var p = MemoryPackSerializer.Deserialize<MobaProjectileEventSnapshotPayload>(payload);
            if (p.Version != CurrentVersion) throw new InvalidOperationException("Unsupported projectile event snapshot version.");
            return p.Entries ?? Array.Empty<MobaProjectileEventSnapshotEntry>();
        }
    }
}
