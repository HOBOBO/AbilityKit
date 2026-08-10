using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Combat.Projectile;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Trajectory;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Eventing;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Services.StateMachine;

namespace AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile
{
    [WorldSystem(order: MobaSystemOrder.ProjectileSync, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileSyncSystem : WorldSystemBase
    {
        private IProjectileService _projectiles;
        private MobaProjectileLinkService _links;
        private MobaActorRegistry _registry;
        private IMobaActorSpawnService _actorSpawn;
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private MobaTriggerExecutionGateway _triggers;
        private IMobaStageTriggerService _stageTriggers;
        private ActorIdAllocator _actorIds;
        private MobaEntityManager _entities;
        private MobaActorSpawnSnapshotService _spawnSnapshots;
        private MobaConfigDatabase _configs;
        private MobaActorDespawnSnapshotService _despawnSnapshots;
        private MobaSkillCastRuntimeService _skillRuntimes;
        private MobaTraceRegistry _trace;
        private AbilityKit.Demo.Moba.Services.MobaProjectileEventSnapshotService _projectileSnapshots;
        private IMobaTemporaryEntityLifecycleService _lifecycle;
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;
        private IMobaBattleDiagnosticEventSink _eventCollector;
        private MobaActorStateMachineFactory _stateMachineFactory;

        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(64);
        private readonly List<ProjectileHitEvent> _hits = new List<ProjectileHitEvent>(128);
        private readonly List<ProjectileTickEvent> _ticks = new List<ProjectileTickEvent>(128);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(64);

        private IProjectileSyncHandler _spawnHandler;
        private IProjectileSyncHandler _tickHandler;
        private IProjectileSyncHandler _exitHandler;
        private IProjectileSyncHandler _hitHandler;

        internal MobaProjectileLinkService Links => _links;
        internal MobaActorRegistry Registry => _registry;
        internal IMobaActorSpawnService ActorSpawn => _actorSpawn;
        internal AbilityKit.Triggering.Eventing.IEventBus EventBus => _eventBus;
        internal MobaTriggerExecutionGateway Triggers => _triggers;
        internal IMobaStageTriggerService StageTriggers => _stageTriggers;
        internal ActorIdAllocator ActorIds => _actorIds;
        internal MobaEntityManager Entities => _entities;
        internal MobaActorSpawnSnapshotService SpawnSnapshots => _spawnSnapshots;
        internal MobaConfigDatabase Configs => _configs;
        internal MobaActorDespawnSnapshotService DespawnSnapshots => _despawnSnapshots;
        internal MobaSkillCastRuntimeService SkillRuntimes => _skillRuntimes;
        internal MobaTraceRegistry Trace => _trace;
        internal AbilityKit.Demo.Moba.Services.MobaProjectileEventSnapshotService ProjectileSnapshots => _projectileSnapshots;
        internal global::ActorContext ActorContext => Contexts.Actor();
        internal IMobaBattleDiagnosticEventSink EventCollector => _eventCollector;
        internal MobaActorStateMachineFactory StateMachineFactory => _stateMachineFactory;

        public MobaProjectileSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _links);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _actorSpawn);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _triggers);
            Services.TryResolve(out _stageTriggers);
            Services.TryResolve(out _actorIds);
            Services.TryResolve(out _entities);
            Services.TryResolve(out _spawnSnapshots);
            Services.TryResolve(out _configs);
            Services.TryResolve(out _despawnSnapshots);
            Services.TryResolve(out _skillRuntimes);
            Services.TryResolve(out _trace);
            Services.TryResolve(out _projectileSnapshots);
            Services.TryResolve(out _lifecycle);
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            Services.TryResolve(out _eventCollector);
            Services.TryResolve(out _stateMachineFactory);

            _spawnHandler = new MobaProjectileSpawnSyncHandler(this);
            _tickHandler = new MobaProjectileTickSyncHandler(this);
            _exitHandler = new MobaProjectileExitSyncHandler(this);
            _hitHandler = new MobaProjectileHitSyncHandler(this);
        }

        protected override void OnExecute()
        {
            if (_projectiles == null || _links == null || _registry == null) return;

            ProcessSpawns();
            ProcessTicks();
            ProcessHits();
            ProcessExits();
        }

        private void ProcessSpawns()
        {
            _spawns.Clear();
            _projectiles.DrainSpawnEvents(_spawns);
            _spawnHandler?.HandleSpawns(_spawns);
        }

        private void ProcessTicks()
        {
            _ticks.Clear();
            _projectiles.DrainTickEvents(_ticks);
            if (_ticks.Count > 0) _lifecycle?.RecordTickEvents(MobaTemporaryEntityKind.Projectile, _ticks.Count);
            _tickHandler?.HandleTicks(_ticks);
        }

        private void ProcessExits()
        {
            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            if (_exits.Count > 0) _lifecycle?.RecordExitEvents(MobaTemporaryEntityKind.Projectile, _exits.Count);
            _exitHandler?.HandleExits(_exits);
        }

        private void ProcessHits()
        {
            _hits.Clear();
            _projectiles.DrainHitEvents(_hits);
            if (_hits.Count > 0) _lifecycle?.RecordHitEvents(MobaTemporaryEntityKind.Projectile, _hits.Count);
            _hitHandler?.HandleHits(_hits);
        }

        internal int ResolveActorIdByCollider(ColliderId id)
        {
            if (_registry == null) return 0;
            if (id.Value <= 0) return 0;

            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var e = kv.Value;
                    if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                    if (e.collisionId.Value.Equals(id))
                    {
                        return e.actorId.Value;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaProjectileSyncSystem] ResolveActorIdByCollider failed");
            }

            return 0;
        }

        internal void RequestDespawn(global::ActorEntity entity, ActorDespawnReason reason, int sourceActorId, long sourceContextId)
        {
            if (entity == null) return;
            if (!TryGetFrame(out var frame))
            {
                throw new System.InvalidOperationException($"MobaProjectileSyncSystem requires authority or frame time before requesting projectile despawn. reason={reason}, sourceActorId={sourceActorId}, sourceContextId={sourceContextId}");
            }

            ActorLifecycleRequests.RequestDespawn(entity, frame, reason, sourceActorId, sourceContextId);
        }

        internal void CleanupProjectileActorOnExit(ProjectileId projectileId, global::ActorEntity entity, ActorDespawnReason reason, int sourceActorId, long sourceContextId)
        {
            if (entity == null) return;
            var actorId = entity.hasActorId ? entity.actorId.Value : 0;
            if (actorId <= 0) return;

            if (_trace != null && sourceContextId != 0L)
            {
                try { _trace.EndContext(sourceContextId, AbilityKit.Trace.TraceLifecycleReason.Completed); }
                catch (System.Exception ex) { Log.Exception(ex, $"[MobaProjectileSyncSystem] end projectile trace failed (projectileId={projectileId.Value}, sourceContextId={sourceContextId})"); }
            }

            if (_skillRuntimes != null && _links != null && _links.TryConsumeRetain(projectileId, out var retainHandle))
            {
                try { _skillRuntimes.ReleaseChild(in retainHandle); }
                catch (System.Exception ex) { Log.Exception(ex, $"[MobaProjectileSyncSystem] release projectile retain failed (projectileId={projectileId.Value})"); }
            }

            _links?.UnlinkByActorId(actorId);
            _despawnSnapshots?.Enqueue(actorId, (byte)reason);
            new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                actorId,
                out _,
                publishDespawn: true);

            try { entity.Destroy(); }
            catch (System.Exception ex) { Log.Exception(ex, $"[MobaProjectileSyncSystem] destroy projectile actor failed (actorId={actorId}, projectileId={projectileId.Value}, reason={reason}, sourceActorId={sourceActorId})"); }
        }

        private bool TryGetFrame(out int frame)
        {
            frame = 0;
            try
            {
                if (_time != null)
                {
                    frame = _time.Frame.Value;
                    return true;
                }

                if (_authority != null)
                {
                    frame = _authority.ConfirmedFrame.Value;
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaProjectileSyncSystem] resolve projectile despawn frame failed");
                return false;
            }

            return false;
        }
    }
}
