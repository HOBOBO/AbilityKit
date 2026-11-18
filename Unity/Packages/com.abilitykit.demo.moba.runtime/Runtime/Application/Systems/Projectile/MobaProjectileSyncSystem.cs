using System;
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
    /// <summary>
    /// MOBA 投射物及区域的共同通知结算入口：每轮冻结全部类别，按既定顺序消费，
    /// 在当前帧内完成派生链。此系统不重新推进物理，失败后阻断当前会话。
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.ProjectileSync, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaProjectileSyncSystem : WorldSystemBase
    {
        private const int MaxSettlementItems = 16384;
        private MobaCombatFrameGuard _frameGuard;
        private AbilityKit.Demo.Moba.Services.Area.MobaAreaRuntimeService _areas;
        private AbilityKit.Demo.Moba.Systems.Area.MobaAreaSyncSystem _areaConsumer;
        private bool _settling;
        private int _consumed;
        private readonly Action<int> _consumeBudget;
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
        private MobaExecutionContextRegistry _executionContexts;
        private AbilityKit.Demo.Moba.Services.MobaProjectileEventSnapshotService _projectileSnapshots;
        private IMobaTemporaryEntityLifecycleService _lifecycle;
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;
        private IMobaBattleDiagnosticEventSink _eventCollector;
        private MobaActorStateMachineFactory _stateMachineFactory;

        private readonly List<ProjectileArrivalEvent> _arrivals = new List<ProjectileArrivalEvent>();
        private readonly List<ProjectileSpawnEvent> _spawns = new List<ProjectileSpawnEvent>(64);
        private readonly List<ProjectileHitEvent> _hits = new List<ProjectileHitEvent>(128);
        private readonly List<ProjectileTickEvent> _ticks = new List<ProjectileTickEvent>(128);
        private readonly List<ProjectileExitEvent> _exits = new List<ProjectileExitEvent>(64);
        private readonly List<int> _transformActorIds = new List<int>(64);

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
        internal MobaExecutionContextRegistry ExecutionContexts => _executionContexts;
        internal AbilityKit.Demo.Moba.Services.MobaProjectileEventSnapshotService ProjectileSnapshots => _projectileSnapshots;
        internal global::ActorContext ActorContext => Contexts.Actor();
        internal IMobaBattleDiagnosticEventSink EventCollector => _eventCollector;
        internal MobaActorStateMachineFactory StateMachineFactory => _stateMachineFactory;

        /// <summary>
        /// 创建共同结算入口，复用原有各类通知处理器及 Area 领域所有权。
        /// </summary>
        public MobaProjectileSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
            _consumeBudget = ConsumeBudget;
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
            Services.TryResolve(out _executionContexts);
            Services.TryResolve(out _projectileSnapshots);
            Services.TryResolve(out _lifecycle);
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            Services.TryResolve(out _eventCollector);
            Services.TryResolve(out _stateMachineFactory);
            Services.TryResolve(out _frameGuard);
            Services.TryResolve(out _areas);
            _areaConsumer = new AbilityKit.Demo.Moba.Systems.Area.MobaAreaSyncSystem(Contexts, Services);
            _areaConsumer.Initialize();

            _spawnHandler = new MobaProjectileSpawnSyncHandler(this);
            _tickHandler = new MobaProjectileTickSyncHandler(this);
            _exitHandler = new MobaProjectileExitSyncHandler(this);
            _hitHandler = new MobaProjectileHitSyncHandler(this);
        }

        protected override void OnExecute()
        {
            try
            {
                if (_settling) throw new InvalidOperationException("MOBA notification settlement cannot reenter.");
                if (_frameGuard?.Failure != null) throw new InvalidOperationException("MOBA session has failed.");
                if (_projectiles is not IProjectilePendingNotifications pending || _links == null || _registry == null || _areas == null || _time == null || _frameGuard == null)
                    throw new InvalidOperationException("MOBA settlement requires notification query, links, actors and Area runtime.");
                _settling = true;
                _consumed = 0;
                var firstRound = true;
                do
                {
                    /*
                     * 先冻结全部类别，再执行任何回调。回调追加的通知只能进入下一轮，
                     * 因而新建并立即删除的对象仍会先 Spawn 后终结。
                     */
                    var projectileCount = FreezeBatch();
                    var areaCount = _areaConsumer.FreezeBatch();
                    ConsumeBudget(projectileCount + areaCount);
                    ProcessSpawns();
                    foreach (var arrival in _arrivals) _stageTriggers?.ExecuteProjectileArrival(in arrival);
                    ProcessTicks();
                    if (firstRound) SyncActiveTransforms();
                    ProcessHits();
                    ProcessExits();
                    _areaConsumer.ConsumeBatch(_consumeBudget);
                    if (_frameGuard.Failure != null) throw new InvalidOperationException("MOBA settlement callback failed.", _frameGuard.Failure);
                    firstRound = false;
                }
                while (pending.PendingNotificationCount != 0 || _areas.HasDueDelayAreas(_time.Frame.Value));
            }
            catch (Exception error)
            {
                _frameGuard?.FailFrame(error);
                throw;
            }
            finally
            {
                _settling = false;
            }
        }

        private void ConsumeBudget(int count)
        {
            if (count > MaxSettlementItems - _consumed)
                throw new InvalidOperationException($"MOBA notification settlement exceeds {MaxSettlementItems} items.");
            _consumed += count;
        }

        private int FreezeBatch()
        {
            _spawns.Clear();
            _projectiles.DrainSpawnEvents(_spawns);
            _arrivals.Clear();
            if (_projectiles is IProjectileArrivalEventSource arrivals) arrivals.DrainArrivalEvents(_arrivals);
            _ticks.Clear();
            _projectiles.DrainTickEvents(_ticks);
            _hits.Clear();
            _projectiles.DrainHitEvents(_hits);
            _exits.Clear();
            _projectiles.DrainExitEvents(_exits);
            return _spawns.Count + _arrivals.Count + _ticks.Count + _hits.Count + _exits.Count;
        }

        private void SyncActiveTransforms()
        {
            /*
             * 游戏 Tick 通知可缺省；Actor 投影仍每帧同步一次，维持 Tick 后 Hit 前的位置。
             */
            _transformActorIds.Clear();
            _transformActorIds.AddRange(_registry.CopyActorIdsInOrder());
            foreach (var actorId in _transformActorIds)
            {
                if (!_links.TryGetProjectileId(actorId, out var projectileId)
                    || !_projectiles.TryGetRuntimeState(projectileId, out var state)
                    || !_registry.TryGet(actorId, out var actor) || actor == null) continue;

                var current = actor.hasTransform ? actor.transform.Value : Transform3.Identity;
                var direction = state.Direction.SqrMagnitude > 0.0001f
                    ? DeterministicMathBridge.Normalize(state.Direction)
                    : current.Forward;
                var position = state.Position;
                var rotation = Quat.LookRotation(direction, Vec3.Up);
                actor.ReplaceTransform(new Transform3(in position, in rotation, in current.Scale));
            }
        }

        private void ProcessSpawns()
        {
            _spawnHandler?.HandleSpawns(_spawns);
        }

        private void ProcessTicks()
        {
            if (_ticks.Count > 0) _lifecycle?.RecordTickEvents(MobaTemporaryEntityKind.Projectile, _ticks.Count);
            _tickHandler?.HandleTicks(_ticks);
        }

        private void ProcessExits()
        {
            if (_exits.Count > 0) _lifecycle?.RecordExitEvents(MobaTemporaryEntityKind.Projectile, _exits.Count);
            _exitHandler?.HandleExits(_exits);
        }

        private void ProcessHits()
        {
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
            RequestDespawn(entity, reason, sourceActorId, sourceContextId);
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
