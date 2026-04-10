using System;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Common.Projectile;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaProjectileEventSnapshotCodec_Obsolete
    {
        public enum EventKind
        {
            Spawn = 1,
            Hit = 2,
            Exit = 3,
        }

        public static byte[] Serialize(Entry[] entries)
        {
            entries ??= Array.Empty<Entry>();
            return BinaryObjectCodec.Encode(new SnapshotPayload(entries));
        }

        public static Entry[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length < 4) return Array.Empty<Entry>();
            var p = BinaryObjectCodec.Decode<SnapshotPayload>(payload);
            return p.Entries ?? Array.Empty<Entry>();
        }

        public readonly struct SnapshotPayload
        {
            [BinaryMember(0)] public readonly Entry[] Entries;

            public SnapshotPayload(Entry[] entries)
            {
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int Kind;
            [BinaryMember(1)] public readonly int ProjectileActorId;
            [BinaryMember(2)] public readonly int OwnerActorId;
            [BinaryMember(3)] public readonly int TemplateId;
            [BinaryMember(4)] public readonly int LauncherActorId;
            [BinaryMember(5)] public readonly int RootActorId;
            [BinaryMember(6)] public readonly float X;
            [BinaryMember(7)] public readonly float Y;
            [BinaryMember(8)] public readonly float Z;
            [BinaryMember(9)] public readonly int HitCollider;
            [BinaryMember(10)] public readonly int ExitReason;

            public Entry(int kind, int projectileActorId, int ownerActorId, int templateId, int launcherActorId, int rootActorId, float x, float y, float z, int hitCollider, int exitReason)
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
            }

            public static Entry FromSpawn(in ProjectileSpawnEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Spawn,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Position.X,
                    y: e.Position.Y,
                    z: e.Position.Z,
                    hitCollider: 0,
                    exitReason: 0);
            }

            public static Entry FromHit(in ProjectileHitEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Hit,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Point.X,
                    y: e.Point.Y,
                    z: e.Point.Z,
                    hitCollider: e.HitCollider.Value,
                    exitReason: 0);
            }

            public static Entry FromExit(in ProjectileExitEvent e)
            {
                return new Entry(
                    kind: (int)EventKind.Exit,
                    projectileActorId: 0,
                    ownerActorId: e.OwnerId,
                    templateId: e.TemplateId,
                    launcherActorId: e.LauncherActorId,
                    rootActorId: e.RootActorId,
                    x: e.Position.X,
                    y: e.Position.Y,
                    z: e.Position.Z,
                    hitCollider: 0,
                    exitReason: (int)e.Reason);
            }
        }
    }
}
