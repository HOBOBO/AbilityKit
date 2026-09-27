using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Share
{
    public static class ProjectileEventSnapshotMapper
    {
        public static ProjectileEventData[] Map(MobaProjectileEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<ProjectileEventData>();
            }

            var events = new ProjectileEventData[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                events[i] = Map(in entries[i]);
            }

            return events;
        }

        public static ProjectileEventData Map(in MobaProjectileEventSnapshotEntry entry)
        {
            return new ProjectileEventData(
                (ProjectilePresentationEventKind)entry.Kind,
                entry.ProjectileActorId,
                entry.OwnerActorId,
                entry.TemplateId,
                entry.LauncherActorId,
                entry.RootActorId,
                entry.X,
                entry.Y,
                entry.Z,
                entry.HitCollider,
                entry.ExitReason,
                entry.ProjectileId,
                entry.ForwardX,
                entry.ForwardY,
                entry.ForwardZ);
        }
    }
}
