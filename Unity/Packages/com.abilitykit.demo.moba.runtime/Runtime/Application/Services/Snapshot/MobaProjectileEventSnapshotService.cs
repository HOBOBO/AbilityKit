using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(40)]
    [WorldService(typeof(MobaProjectileEventSnapshotService))]
    public sealed class MobaProjectileEventSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaProjectileLinkService _links;
 
        private FrameIndex _lastFrame;
 
        private readonly MobaSnapshotBuffer<MobaProjectileEventSnapshotEntry> _projectileEntries = new MobaSnapshotBuffer<MobaProjectileEventSnapshotEntry>(32, 512);

        public MobaProjectileEventSnapshotService(MobaLogicWorldRunGateService phase, IProjectileService projectiles, MobaProjectileLinkService links)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _links = links;
            _lastFrame = new FrameIndex(-999999);
        }

        public void RecordSpawn(in ProjectileSpawnEvent e)
        {
            var entry = FromSpawn(in e);
            PopulateProjectileActorId(e.Projectile, ref entry);
            _projectileEntries.Add(entry);
        }

        public void RecordHit(in ProjectileHitEvent e)
        {
            var entry = FromHit(in e);
            PopulateProjectileActorId(e.Projectile, ref entry);
            _projectileEntries.Add(entry);
        }

        public void RecordExit(in ProjectileExitEvent e)
        {
            var entry = FromExit(in e);
            PopulateProjectileActorId(e.Projectile, ref entry);
            _projectileEntries.Add(entry);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            if (_projectileEntries.Count == 0)
            {
                snapshot = default;
                return false;
            }
 
            var payload = MobaProjectileEventSnapshotCodec.Serialize(_projectileEntries.ToArrayClearAndTrim());
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ProjectileEvent, payload);
            return true;
        }

        private static MobaProjectileEventSnapshotEntry FromSpawn(in ProjectileSpawnEvent e)
        {
            return new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProjectileEventKind.Spawn,
                ProjectileActorId = 0,
                OwnerActorId = e.OwnerId,
                TemplateId = e.TemplateId,
                LauncherActorId = e.LauncherActorId,
                RootActorId = e.RootActorId,
                X = e.Position.X,
                Y = e.Position.Y,
                Z = e.Position.Z,
                HitCollider = 0,
                ExitReason = 0,
                ProjectileId = e.Projectile.Value,
                ForwardX = e.Direction.X,
                ForwardY = e.Direction.Y,
                ForwardZ = e.Direction.Z
            };
        }

        private static MobaProjectileEventSnapshotEntry FromHit(in ProjectileHitEvent e)
        {
            return new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProjectileEventKind.Hit,
                ProjectileActorId = 0,
                OwnerActorId = e.OwnerId,
                TemplateId = e.TemplateId,
                LauncherActorId = e.LauncherActorId,
                RootActorId = e.RootActorId,
                X = e.Point.X,
                Y = e.Point.Y,
                Z = e.Point.Z,
                HitCollider = e.HitCollider.Value,
                ExitReason = 0,
                ProjectileId = e.Projectile.Value
            };
        }

        private static MobaProjectileEventSnapshotEntry FromExit(in ProjectileExitEvent e)
        {
            return new MobaProjectileEventSnapshotEntry
            {
                Kind = (int)ProjectileEventKind.Exit,
                ProjectileActorId = 0,
                OwnerActorId = e.OwnerId,
                TemplateId = e.TemplateId,
                LauncherActorId = e.LauncherActorId,
                RootActorId = e.RootActorId,
                X = e.Position.X,
                Y = e.Position.Y,
                Z = e.Position.Z,
                HitCollider = 0,
                ExitReason = (int)e.Reason,
                ProjectileId = e.Projectile.Value
            };
        }

        private void PopulateProjectileActorId(ProjectileId projectile, ref MobaProjectileEventSnapshotEntry entry)
        {
            if (_links != null && _links.TryGetActorId(projectile, out var projectileActorId) && projectileActorId > 0)
            {
                entry.ProjectileActorId = projectileActorId;
                return;
            }

            if (_links != null && _links.TryGetRetiredActorId(projectile, out projectileActorId) && projectileActorId > 0)
            {
                entry.ProjectileActorId = projectileActorId;
            }
        }

        public void Dispose()
        {
            _projectileEntries.ClearAndTrim();
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
