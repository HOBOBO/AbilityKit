using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaProjectileEventSnapshotService_Obsolete2 : IService
    {
        private readonly MobaGamePhaseService _phase;
        private readonly IProjectileService _projectiles;
        private readonly MobaProjectileLinkService _links;

        private FrameIndex _lastFrame;

        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(32);
        private readonly List<ProjectileHitEvent> _hits = new List<ProjectileHitEvent>(32);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(32);

        public MobaProjectileEventSnapshotService_Obsolete2(MobaGamePhaseService phase, IProjectileService projectiles, MobaProjectileLinkService links)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            _links = links;
            _lastFrame = new FrameIndex(-999999);
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

            _spawns.Clear();
            _hits.Clear();
            _exits.Clear();

            _projectiles.DrainSpawnEvents(_spawns);
            _projectiles.DrainHitEvents(_hits);
            _projectiles.DrainExitEvents(_exits);

            if (_spawns.Count == 0 && _hits.Count == 0 && _exits.Count == 0)
            {
                snapshot = default;
                return false;
            }

            var entries = new List<MobaProjectileEventSnapshotCodec.Entry>(_spawns.Count + _hits.Count + _exits.Count);

            for (int i = 0; i < _spawns.Count; i++)
            {
                var e = _spawns[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromSpawn(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            for (int i = 0; i < _hits.Count; i++)
            {
                var e = _hits[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromHit(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            for (int i = 0; i < _exits.Count; i++)
            {
                var e = _exits[i];
                var it = MobaProjectileEventSnapshotCodec.Entry.FromExit(in e);
                if (_links != null && _links.TryGetActorId(e.Projectile, out var projectileActorId) && projectileActorId > 0)
                {
                    it = new MobaProjectileEventSnapshotCodec.Entry(
                        kind: it.Kind,
                        projectileActorId: projectileActorId,
                        ownerActorId: it.OwnerActorId,
                        templateId: it.TemplateId,
                        launcherActorId: it.LauncherActorId,
                        rootActorId: it.RootActorId,
                        x: it.X,
                        y: it.Y,
                        z: it.Z,
                        hitCollider: it.HitCollider,
                        exitReason: it.ExitReason);
                }
                entries.Add(it);
            }

            var payload = MobaProjectileEventSnapshotCodec.Serialize(entries.ToArray());
            snapshot = new WorldStateSnapshot((int)MobaOpCode.ProjectileEventSnapshot, payload);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
