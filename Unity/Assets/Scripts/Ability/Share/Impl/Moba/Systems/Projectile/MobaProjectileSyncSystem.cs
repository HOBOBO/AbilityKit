using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaProjectileLinkService _links;
        private MobaActorRegistry _registry;
        private ActorIdAllocator _actorIds;
        private MobaEntityManager _entities;

        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(64);
        private readonly List<ProjectileTickEvent> _ticks = new List<ProjectileTickEvent>(128);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(64);

        public MobaProjectileSyncSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _projectiles);
            Services.TryGet(out _links);
            Services.TryGet(out _registry);
            Services.TryGet(out _actorIds);
            Services.TryGet(out _entities);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _links == null || _registry == null) return;

            _spawns.Clear();
            _projectiles.DrainSpawnEvents(_spawns);
            for (int i = 0; i < _spawns.Count; i++)
            {
                var evt = _spawns[i];

                // MobaProjectileService immediate-shoot path already creates ActorEntity and links it.
                if (_links.TryGetActorId(evt.Projectile, out var existingActorId) && existingActorId > 0) continue;
                if (_actorIds == null) continue;
                if (_entities == null) continue;

                if (!_entities.TryGetActorEntity(evt.OwnerId, out var caster) || caster == null || !caster.hasTransform)
                {
                    continue;
                }

                var projectileActorId = _actorIds.Next();

                var dir = evt.Direction;
                if (dir.SqrMagnitude <= 0f) dir = Vec3.Forward;
                dir = dir.Normalized;

                var rot = Quat.LookRotation(dir, Vec3.Up);
                var t = new Transform3(evt.Position, rot, Vec3.One);

                var team = caster.hasTeam ? caster.team.Value : Team.None;
                var ownerPlayer = caster.hasOwnerPlayerId ? caster.ownerPlayerId.Value : default(PlayerId);

                var info = new MobaEntityInfo(
                    actorId: projectileActorId,
                    kind: MobaEntityKind.Projectile,
                    transform: t,
                    team: team,
                    mainType: EntityMainType.Projectile,
                    unitSubType: UnitSubType.Bullet,
                    ownerPlayer: ownerPlayer,
                    templateId: evt.TemplateId);

                var bullet = MobaEntitySpawnFactory.Create(Contexts.actor, in info);
                if (bullet == null) continue;

                bullet.isFlyingProjectileTag = true;

                _registry.Register(projectileActorId, bullet);

                try { _entities.TryRegisterFromEntity(bullet); }
                catch { }

                _links.Link(evt.Projectile, projectileActorId);

                if (evt.LauncherActorId > 0 && _registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
                {
                    var plc = launcherEntity.projectileLauncher;
                    launcherEntity.ReplaceProjectileLauncher(
                        newLauncherId: plc.LauncherId,
                        newProjectileId: plc.ProjectileId,
                        newRootActorId: plc.RootActorId,
                        newEndTimeMs: plc.EndTimeMs,
                        newActiveBullets: plc.ActiveBullets + 1,
                        newScheduleId: plc.ScheduleId,
                        newIntervalFrames: plc.IntervalFrames,
                        newTotalCount: plc.TotalCount);
                }
            }

            _ticks.Clear();
            _projectiles.DrainTickEvents(_ticks);
            for (int i = 0; i < _ticks.Count; i++)
            {
                var evt = _ticks[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (!_registry.TryGet(actorId, out var e) || e == null) continue;
                if (!e.hasTransform) continue;

                var t = e.transform.Value;
                var nt = new Transform3(evt.Position, t.Rotation, t.Scale);
                e.ReplaceTransform(nt);
            }

            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            for (int i = 0; i < _exits.Count; i++)
            {
                var evt = _exits[i];
                if (!_links.TryGetActorId(evt.Projectile, out var actorId) || actorId <= 0) continue;
                if (_registry.TryGet(actorId, out var e) && e != null)
                {
                    try { e.Destroy(); }
                    catch { }
                }

                if (evt.LauncherActorId > 0 && _registry.TryGet(evt.LauncherActorId, out var launcherEntity) && launcherEntity != null && launcherEntity.hasProjectileLauncher)
                {
                    var plc = launcherEntity.projectileLauncher;
                    var next = plc.ActiveBullets - 1;
                    if (next < 0) next = 0;
                    launcherEntity.ReplaceProjectileLauncher(
                        newLauncherId: plc.LauncherId,
                        newProjectileId: plc.ProjectileId,
                        newRootActorId: plc.RootActorId,
                        newEndTimeMs: plc.EndTimeMs,
                        newActiveBullets: next,
                        newScheduleId: plc.ScheduleId,
                        newIntervalFrames: plc.IntervalFrames,
                        newTotalCount: plc.TotalCount);
                }

                _registry.Unregister(actorId);
                _links.UnlinkByProjectileId(evt.Projectile);
            }
        }
    }
}
