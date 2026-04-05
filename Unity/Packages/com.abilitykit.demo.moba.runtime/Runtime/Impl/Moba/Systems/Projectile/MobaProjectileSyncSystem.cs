using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Trajectory;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Core.Eventing;
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
        private AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private MobaEffectExecutionService _effects;
        private ActorIdAllocator _actorIds;
        private MobaEntityManager _entities;
        private MobaActorSpawnSnapshotService _spawnSnapshots;
        private MobaConfigDatabase _configs;
        private MobaActorDespawnSnapshotService _despawnSnapshots;
        
        // [REMOVED] private EffectRegistry _effectRegistry; // TODO: Effects包已删除，待重构

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
        internal AbilityKit.Triggering.Eventing.IEventBus EventBus => _eventBus;
        internal MobaEffectExecutionService Effects => _effects;
        internal ActorIdAllocator ActorIds => _actorIds;
        internal MobaEntityManager Entities => _entities;
        internal MobaActorSpawnSnapshotService SpawnSnapshots => _spawnSnapshots;
        internal MobaConfigDatabase Configs => _configs;
        internal MobaActorDespawnSnapshotService DespawnSnapshots => _despawnSnapshots;
        // [REMOVED] internal EffectRegistry EffectRegistry => _effectRegistry;
        internal global::ActorContext ActorContext => Contexts.Actor();

        public MobaProjectileSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _projectiles);
            Services.TryResolve(out _links);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _effects);
            Services.TryResolve(out _actorIds);
            Services.TryResolve(out _entities);
            Services.TryResolve(out _spawnSnapshots);
            Services.TryResolve(out _configs);
            Services.TryResolve(out _despawnSnapshots);
            // [REMOVED] Services.TryResolve(out _effectRegistry);

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
            ProcessExits();
            ProcessHits();
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
            _tickHandler?.HandleTicks(_ticks);
        }

        private void ProcessExits()
        {
            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            _exitHandler?.HandleExits(_exits);
        }

        private void ProcessHits()
        {
            _hits.Clear();
            _projectiles.DrainHitEvents(_hits);
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
    }
}

