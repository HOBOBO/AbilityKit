using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Pipeline;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 同次权威提交的普通移动截面。完整朝向和持续输入来自逻辑世界，不从投影速度推导。
    /// </summary>
    internal readonly struct LocalStateSyncMovementCut
    {
        private readonly LocalStateSyncCastStamp _stamp;
        internal readonly MobaAuthoritativeMovementAnchor State;
        internal readonly bool HasMoveInput;

        private LocalStateSyncMovementCut(in LocalStateSyncCastStamp stamp,
            in MobaAuthoritativeMovementAnchor state, bool hasMoveInput)
        {
            _stamp = stamp;
            State = state;
            HasMoveInput = hasMoveInput;
        }

        internal static LocalStateSyncMovementCut Capture(EntitasWorld world, in LocalStateSyncCastStamp stamp)
        {
            var services = world.Services;
            if (services.Resolve<IFrameTime>().Frame.Value != stamp.Tick ||
                !TryRead(services, in stamp.Scope, out var state) ||
                state.ActorId != stamp.Scope.ActorId || state.EntityVersion != stamp.Scope.EntityVersion || !state.CanMove ||
                !services.Resolve<MobaActorRegistry>().TryGet(state.ActorId, out var actor) ||
                (!state.HasIsolatedLocomotion && !(stamp.Tick == 0 && IsFreshIsolatedMotion(actor) &&
                    state.MoveX == 0f && state.MoveZ == 0f)))
                throw new InvalidOperationException("当前权威刻不具备隔离的普通移动截面。");
            return new LocalStateSyncMovementCut(in stamp, in state, actor.hasMoveInput);
        }

        internal bool Matches(in LocalStateSyncCastStamp stamp, in MobaActorStateImport actor) =>
            _stamp.Matches(in stamp) && State.Pose.Position.Equals(new Vec3(actor.PosX, actor.PosY, actor.PosZ));

        internal static bool IsFreshIsolatedMotion(ActorEntity actor) => actor.hasMotion &&
            !actor.motion.Initialized && actor.motion.Pipeline == null &&
            ReferenceEquals(actor.motion.Solver, NoMotionSolver.Instance) &&
            actor.motion.Policy == null && actor.motion.Events == null;

        internal static bool TryRead(IWorldResolver services, in LocalStateSyncCastScope scope,
            out MobaAuthoritativeMovementAnchor state) => MobaAuthoritativeMovementAnchor.TryCapture(services,
                new PlayerId(scope.PlayerId.ToString(CultureInfo.InvariantCulture)), out state);
    }

    /// <summary>
    /// 已结算截面的槽位装载身份与绝对冷却，不携带活跃施法或资源事务。
    /// </summary>
    internal readonly struct LocalStateSyncCastSlot
    {
        internal readonly int Slot, SkillId, Level, DurationMs;
        internal readonly long EndMs;

        internal LocalStateSyncCastSlot(int slot, int skillId, int level, long endMs, int durationMs)
        {
            Slot = slot;
            SkillId = skillId;
            Level = level;
            EndMs = endMs;
            DurationMs = durationMs;
        }
    }

    /// <summary>
    /// 两槽离线经济观测；不能单独证明该刻没有在途施法或其他恢复依赖。
    /// </summary>
    internal readonly struct LocalStateSyncCastEconomy
    {
        internal readonly long ManaRaw;
        internal readonly LocalStateSyncCastSlot First, Second;

        internal LocalStateSyncCastEconomy(long manaRaw, LocalStateSyncCastSlot first, LocalStateSyncCastSlot second)
        {
            ManaRaw = manaRaw;
            First = first;
            Second = second;
        }
    }

    /// <summary>
    /// 离线供给方对同次权威提交的施法恢复覆盖声明。默认值表示未知，不等同于无在途施法。
    /// </summary>
    internal readonly struct LocalStateSyncBattleCastCoverage
    {
        private readonly LocalStateSyncCastStamp? _settledStamp;
        internal readonly LocalStateSyncWindupCut Windup;

        private LocalStateSyncBattleCastCoverage(LocalStateSyncCastStamp? settledStamp, LocalStateSyncWindupCut windup)
        {
            _settledStamp = settledStamp;
            Windup = windup;
        }

        /// <summary>
        /// 离线供给方已确认同一刻没有在途施法，且配方排除了未来调度和其他未覆盖依赖。
        /// </summary>
        internal static LocalStateSyncBattleCastCoverage Settled(in LocalStateSyncCastStamp stamp) => new(stamp, null);

        internal static LocalStateSyncBattleCastCoverage InFlight(LocalStateSyncWindupCut windup) =>
            new(null, windup ?? throw new ArgumentNullException(nameof(windup)));

        internal bool Matches(in LocalStateSyncCastStamp stamp) => Windup != null
            ? Windup.Matches(in stamp)
            : _settledStamp is { } settledStamp && settledStamp.Matches(in stamp);
    }

    /// <summary>
    /// 角色状态来自已提交快照，经济状态来自同一次提交的独立类型化截面。
    /// </summary>
    internal readonly struct LocalStateSyncBattleCastCut
    {
        internal readonly int Tick;
        internal readonly MobaActorStateImport Actor;
        internal readonly LocalStateSyncCastEconomy Economy;
        internal readonly LocalStateSyncWindupCut Windup;

        internal readonly LocalStateSyncMovementCut? Movement;
        internal readonly LocalStateSyncProjectileCut Projectile;

        internal LocalStateSyncBattleCastCut(int tick, MobaActorStateImport actor, LocalStateSyncCastEconomy economy,
            LocalStateSyncWindupCut windup = null, LocalStateSyncMovementCut? movement = null,
            LocalStateSyncProjectileCut projectile = null)
        {
            Tick = tick;
            Actor = actor;
            Economy = economy;
            Windup = windup;
            Movement = movement;
            Projectile = projectile;
        }

        internal LocalStateSyncBattleCastObservation Observe() => new LocalStateSyncBattleCastObservation(
            Tick, Economy, new Vec3(Actor.PosX, Actor.PosY, Actor.PosZ), Windup?.ElapsedMs, Movement?.State.Pose.Rotation,
            Projectile?.Observation);
    }

    internal readonly struct LocalStateSyncFlightObservation
    {
        internal readonly int ProjectileId, ActorId, EntityVersion, LifetimeFramesLeft;
        internal readonly long DistanceLeftRaw;
        internal readonly Transform3 Pose;

        internal LocalStateSyncFlightObservation(ProjectileService.LocalFlightCut flight, int actorId, int version, Transform3 pose)
        {
            ProjectileId = flight.ActiveId.GetValueOrDefault().Value;
            ActorId = actorId;
            EntityVersion = version;
            LifetimeFramesLeft = flight.LifetimeFramesLeft;
            DistanceLeftRaw = flight.DistanceLeftRaw;
            Pose = pose;
        }
    }

    /// <summary>完整的本地零／单弹体名单；与输入终结证明独立。</summary>
    internal sealed class LocalStateSyncProjectileCut
    {
        private readonly LocalStateSyncCastStamp _stamp;
        private readonly int _mask, _nextActorId, _version, _team, _ownerCollider;
        private readonly int? _modelId;
        private readonly Transform3 _pose;
        private readonly ProjectileService.LocalFlightCut _flight;
        private readonly MobaProjectileLinkRollbackState _links;
        private readonly MobaSkillCastRuntimeServiceSnapshot _casts;
        private readonly MobaSkillEconomyServiceSnapshot _economy;
        private readonly IReadOnlyList<MobaExecutionContextNode> _contexts;
        private readonly long _nextContext;

        private LocalStateSyncProjectileCut(EntitasWorld world, in LocalStateSyncCastStamp stamp, int mask, ActorEntity owner)
        {
            _stamp = stamp;
            _mask = mask;
            _ownerCollider = ColliderOf(owner);
            var services = world.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            _flight = ReadFlight(world, stamp.Scope.ActorId, mask, owner, out var projectile);
            _links = services.Resolve<MobaProjectileLinkService>().CaptureRollbackState();
            _nextActorId = services.Resolve<ActorIdAllocator>().NextId;
            _casts = services.Resolve<MobaSkillCastRuntimeService>().CaptureRollbackSnapshot();
            _economy = services.Resolve<MobaSkillEconomyService>().CaptureRollbackSnapshot();
            var registry = services.Resolve<MobaExecutionContextRegistry>();
            _contexts = registry.CaptureLifecycleSnapshot();
            _nextContext = registry.NextContextId;
            if (services.Resolve<SkillCastCoordinator>().CaptureRunnerState(typed: true).Entries.Length != 0 ||
                _economy.Transactions.Length != 0 || _economy.Cooldowns.Length != 0 ||
                _economy.GlobalCooldowns.Length != 0 || _economy.KillRefunds.Length != 0 ||
                _links.Launchers.Length != 0 || _nextActorId <= stamp.Scope.ActorId ||
                _casts.Retains.Length != _links.Projectiles.Length || _casts.Runtimes.Length != _links.Projectiles.Length)
                throw new InvalidOperationException("弹体截面仅覆盖已结束施法管线和单一弹体保留引用。");

            if (projectile != null)
            {
                var entry = _links.Projectiles[0];
                _version = actors.GetEntityVersion(entry.ActorId);
                _pose = projectile.transform.Value;
                _team = (int)projectile.team.Value;
                _modelId = projectile.hasModelId ? projectile.modelId.Value : (int?)null;
                var retained = _casts.Runtimes[0];
                var handle = new MobaSkillCastRuntimeHandle(retained.RuntimeId, retained.Generation, retained.RootContextId);
                if (_version <= 0 || entry.ActorId >= _nextActorId || !entry.Source.IsValid ||
                    entry.Source.SourceActorId != stamp.Scope.ActorId || entry.Source.InitialTargetActorId != 0 ||
                    entry.Source.ProjectileConfigId != _flight.TemplateId || !entry.Retain.IsValid ||
                    !entry.Retain.Equals(_casts.Retains[0]) || !handle.Equals(entry.Source.SkillRuntimeHandle) ||
                    !handle.Equals(entry.Retain.Runtime) || entry.Retain.Child.Kind != MobaSkillRuntimeChildKind.Projectile ||
                    entry.Retain.Child.ChildId != entry.ProjectileId || entry.Retain.Child.ContextId != entry.Source.SourceContextId ||
                    entry.Retain.Child.ConfigId != _flight.TemplateId || entry.ConfirmedKills != 0 ||
                    !retained.PipelineEnded || retained.IsEnded || retained.CasterActorId != stamp.Scope.ActorId ||
                    retained.Children.Length != 1 || !retained.Children[0].Equals(entry.Retain.Child) ||
                    !_contexts.Any(node => node.ContextId == handle.RootContextId && node.Kind == MobaExecutionKind.SkillCast &&
                        node.SourceActorId == _stamp.Scope.ActorId) ||
                    !_contexts.Any(node => node.ContextId == entry.Source.SourceContextId && !node.IsEnded &&
                        node.Kind == MobaExecutionKind.ProjectileLaunch && node.RootContextId == handle.RootContextId &&
                        node.SourceActorId == _stamp.Scope.ActorId && node.ConfigId == _flight.TemplateId))
                    throw new InvalidOperationException("弹体来源、施法身份或保留引用不完整。");
            }
        }

        internal static LocalStateSyncProjectileCut Capture(EntitasWorld world, in LocalStateSyncCastStamp stamp, int mask)
        {
            var services = world.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            if (services.Resolve<IFrameTime>().Frame.Value != stamp.Tick ||
                !actors.MatchesEntityVersion(stamp.Scope.ActorId, stamp.Scope.EntityVersion) ||
                !actors.TryGet(stamp.Scope.ActorId, out var owner))
                throw new InvalidOperationException("弹体截面不匹配权威 Tick 或角色生命周期。");
            return new LocalStateSyncProjectileCut(world, in stamp, mask, owner);
        }

        internal bool Matches(in LocalStateSyncCastStamp stamp, int mask, GatewayStateSyncActorSnapshot[] actors)
        {
            if (!_stamp.Matches(in stamp) || _mask != mask || actors == null) return false;
            if (!_flight.ActiveId.HasValue) return actors.Length == 1;
            if (actors.Length != 2) return false;
            var entry = _links.Projectiles[0];
            return actors.Any(actor => actor.ActorId == entry.ActorId && actor.Kind == (int)SpawnEntityKind.Projectile &&
                actor.Code == _flight.TemplateId && actor.TeamId == _team &&
                new Vec3(actor.X, actor.Y, actor.Z).Equals(_pose.Position));
        }

        internal bool MatchesScope(in LocalStateSyncCastScope scope, int tick, int mask) =>
            _stamp.Tick == tick && _mask == mask && _stamp.Scope.Matches(in scope);

        internal LocalStateSyncFlightObservation? Observation => _flight.ActiveId.HasValue
            ? new LocalStateSyncFlightObservation(_flight, _links.Projectiles[0].ActorId, _version, _pose)
            : (LocalStateSyncFlightObservation?)null;

        internal void Restore(EntitasWorld world, ActorEntity owner)
        {
            var services = world.Services;
            var registry = services.Resolve<MobaActorRegistry>();
            var service = ResolveProjectile(services);
            var allocator = services.Resolve<ActorIdAllocator>();
            EnsureIsolated(services, _mask);
            if (allocator.NextId > _nextActorId || service.ActiveCount != 0 || ColliderOf(owner) != _ownerCollider ||
                services.Resolve<MobaProjectileLinkService>().ActiveCount != 0)
                throw new InvalidOperationException("弹体候选不是空世界。");

            IProjectileHitFilter filter = null;
            if (_flight.ActiveId.HasValue)
            {
                var entry = _links.Projectiles[0];
                var config = ReadConfig(services, _flight.TemplateId, _mask);
                filter = new MobaProjectileCollisionFilter(registry, config.HitUnitSubType);
                var spec = MobaConverter.ToProjectileActorBuildSpec(entry.ActorId, config.Id, owner, _pose.Position, _pose.Forward);
                var info = new MobaEntityInfo(entry.ActorId, spec.Info.Kind, in _pose, spec.Info.Team,
                    spec.Info.MainType, spec.Info.UnitSubType, spec.Info.OwnerPlayer, config.Id);
                var actor = ActorArchetypeFactory.Create(((global::Contexts)world.Contexts).actor, in info);
                if (_modelId.HasValue) actor.AddModelId(_modelId.Value);
                actor.isFlyingProjectileTag = true;
                registry.RegisterRestored(entry.ActorId, actor, _version);
                if (!services.Resolve<MobaEntityManager>().RegisterSilently(entry.ActorId, actor, info.Team,
                        info.MainType, info.UnitSubType, info.OwnerPlayer, publishObjectLifecycle: false))
                    throw new InvalidOperationException("弹体候选 Actor 索引恢复失败。");
                if (!services.Resolve<MobaActorSpawnService>().TryRegisterCollision(actor))
                    throw new InvalidOperationException("弹体候选碰撞形状恢复失败。");
            }

            service.RestoreLocalFlightCut(new FrameIndex(_stamp.Tick), _flight, _mask, filter, ColliderOf(owner));
            services.Resolve<MobaExecutionContextRegistry>().RestoreLocalRegistry(_contexts, _nextContext);
            services.Resolve<MobaSkillCastRuntimeService>().RestoreRollbackSnapshot(in _casts);
            services.Resolve<MobaSkillEconomyService>().RestoreRollbackSnapshot(in _economy);
            services.Resolve<MobaProjectileLinkService>().RestoreRollbackState(_links);
            allocator.Reset(_nextActorId);
        }

        internal static ProjectileService.LocalFlightCut ReadFlight(EntitasWorld world, int ownerId, int mask,
            ActorEntity owner, out ActorEntity projectile)
        {
            projectile = null;
            var services = world.Services;
            EnsureIsolated(services, mask);
            var actors = services.Resolve<MobaActorRegistry>();
            var links = services.Resolve<MobaProjectileLinkService>();
            MobaProjectileCollisionFilter captured = null;
            var flight = ResolveProjectile(services).CaptureLocalFlightCut(services.Resolve<IFrameTime>().Frame,
                mask, filter => (captured = filter as MobaProjectileCollisionFilter) != null, ColliderOf(owner));
            if (actors.CopyActorIdsInOrder().Count != (flight.ActiveId.HasValue ? 2 : 1) ||
                links.ActiveCount != (flight.ActiveId.HasValue ? 1 : 0))
                throw new InvalidOperationException("世界名单超出单施法者弹体覆盖。");
            if (flight.ActiveId is { } id)
            {
                var config = ReadConfig(services, flight.TemplateId, mask);
                if (captured == null || !captured.MatchesLocalFlight(actors, config.HitUnitSubType) || flight.OwnerId != ownerId ||
                    !links.TryGetActorId(id, out var actorId) || actorId == ownerId ||
                    !actors.TryGet(actorId, out projectile) || !projectile.isFlyingProjectileTag ||
                    !projectile.hasTransform || !projectile.hasCollisionLayer ||
                    projectile.hasActorStateMachine || !projectile.hasCollisionId || projectile.hasOwnerLink ||
                    (projectile.collisionLayer.Mask & mask) != 0)
                    throw new InvalidOperationException("单弹体名单与正式飞行 Actor 不匹配。");
            }
            return flight;
        }

        private static int ColliderOf(ActorEntity owner) => owner.hasCollisionId ? owner.collisionId.Value.Value : 0;

        private static ProjectileService ResolveProjectile(IWorldResolver services) =>
            services.Resolve<IProjectileService>() as ProjectileService ??
            throw new InvalidOperationException("本地截面需要正式弹体服务。");

        private static void EnsureIsolated(IWorldResolver services, int mask)
        {
            if (mask == 0 || (mask & (mask - 1)) != 0 ||
                services.Resolve<ICollisionService>().World is not ICollisionWorldDebugView view)
                throw new InvalidOperationException("本地弹体需要可检查的单一隔离碰撞层。");
            var shapes = new List<CollisionWorldDebugShape>();
            view.CopyWorldShapes(shapes);
            if (shapes.Exists(shape => (mask & (1 << shape.LayerId)) != 0))
                throw new InvalidOperationException("隔离弹体层存在碰撞体。");
        }

        private static ProjectileMO ReadConfig(IWorldResolver services, int id, int mask)
        {
            if (!services.Resolve<MobaConfigDatabase>().TryGetProjectile(id, out var config) || config == null ||
                config.CollisionLayerMask != mask || config.CollisionRules.Count != 0 || config.OnHitEffectId != 0 ||
                config.OnSpawnTriggerIds.Length != 0 || config.OnHitTriggerIds.Length != 0 ||
                config.OnTickTriggerIds.Length != 0 || config.OnExitTriggerIds.Length != 0 ||
                config.OnArrivalTriggerIds.Length != 0 || !string.IsNullOrEmpty(config.StateMachineProfileId))
                throw new InvalidOperationException("弹体配方包含未覆盖的玩法依赖。");
            return config;
        }
    }

    /// <summary>
    /// 同一已提交刻的离线前摇数据。配方负责排除外部事件、角色模式、Buff 和未来调度；
    /// 本截面不赋予任意战斗世界恢复资格。
    /// </summary>
    internal sealed class LocalStateSyncWindupCut
    {
        private readonly LocalStateSyncCastStamp _stamp;
        private readonly SkillRunnerRollbackSnapshot _runner;
        private readonly MobaSkillCastRuntimeServiceSnapshot _runtime;
        private readonly MobaSkillEconomyServiceSnapshot _transactions;
        private readonly System.Collections.Generic.IReadOnlyList<MobaExecutionContextNode> _nodes;
        private readonly long _nextContextId;
        private readonly bool _firstSlotInitialized, _secondSlotInitialized;
        internal readonly LocalStateSyncCastEconomy Economy;
        internal readonly int ElapsedMs;

        private LocalStateSyncWindupCut(in LocalStateSyncCastStamp stamp, in LocalStateSyncCastEconomy economy,
            SkillRunnerRollbackSnapshot runner, in MobaSkillCastRuntimeServiceSnapshot runtime,
            in MobaSkillEconomyServiceSnapshot transactions, MobaExecutionContextRegistry contexts,
            bool firstSlotInitialized, bool secondSlotInitialized, int elapsedMs)
        {
            _stamp = stamp;
            Economy = economy;
            _runner = runner;
            _runtime = runtime;
            _transactions = transactions;
            _nodes = contexts.CaptureLifecycleSnapshot();
            _nextContextId = contexts.NextContextId;
            _firstSlotInitialized = firstSlotInitialized;
            _secondSlotInitialized = secondSlotInitialized;
            ElapsedMs = elapsedMs;
        }

        internal bool Matches(in LocalStateSyncCastStamp stamp) => _stamp.Matches(in stamp);

        /// <summary>
        /// 从隔离权威配方的已完成 Tick 捕获单一前摇，拒绝外部目标、派生对象和未覆盖的经济机制。
        /// </summary>
        internal static LocalStateSyncWindupCut Capture(EntitasWorld authority, in LocalStateSyncCastStamp stamp,
            int firstSlot, int secondSlot)
        {
            var services = authority.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            var actorId = stamp.Scope.ActorId;
            if (services.Resolve<IFrameTime>().Frame.Value != stamp.Tick || actors.CopyActorIdsInOrder().Count != 1 ||
                !actors.MatchesEntityVersion(actorId, stamp.Scope.EntityVersion) || !actors.TryGet(actorId, out var actor))
                throw new InvalidOperationException("前摇截面不匹配权威 Tick 或角色生命周期。");

            var economy = new LocalStateSyncCastEconomy(actor.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
                ReadSlot(firstSlot, out var firstSlotInitialized), ReadSlot(secondSlot, out var secondSlotInitialized));
            var cast = services.Resolve<SkillCastCoordinator>();
            var runner = cast.CaptureRunnerState(typed: true);
            var runtime = services.Resolve<MobaSkillCastRuntimeService>().CaptureRollbackSnapshot();
            var transactions = services.Resolve<MobaSkillEconomyService>().CaptureRollbackSnapshot();
            if (runner.Entries.Length != 1 || runtime.Runtimes.Length != 1 || runtime.Retains.Length != 0 ||
                transactions.Transactions.Length > 1 || transactions.Cooldowns.Length != 0 ||
                transactions.GlobalCooldowns.Length != 0 || transactions.KillRefunds.Length > transactions.Transactions.Length)
                throw new InvalidOperationException($"前摇截面存在未覆盖的战斗依赖：施法 {runner.Entries.Length}，" +
                    $"运行实例 {runtime.Runtimes.Length}，保留引用 {runtime.Retains.Length}，事务 {transactions.Transactions.Length}，" +
                    $"共享冷却 {transactions.Cooldowns.Length}，全局冷却 {transactions.GlobalCooldowns.Length}，退款 {transactions.KillRefunds.Length}。");

            var entry = runner.Entries[0];
            var running = runtime.Runtimes[0];
            var handle = new MobaSkillCastRuntimeHandle(entry.RuntimeId, entry.RuntimeGeneration, entry.RootContextId);
            if (entry.Stage != 0 || running.Stage != SkillCastStage.PreCast || running.PipelineEnded ||
                entry.ActorId != actorId || running.CasterActorId != actorId ||
                (entry.SkillSlot != firstSlot && entry.SkillSlot != secondSlot) ||
                (entry.Context.TargetActorId != 0 && entry.Context.TargetActorId != actorId) ||
                running.Children.Length != 0 || running.RuntimeId != entry.RuntimeId ||
                running.Generation != entry.RuntimeGeneration || running.RootContextId != entry.RootContextId ||
                entry.Context.CharacterModeOwnerId != 0 || entry.Context.HfsmParameterOwnerId != 0)
                throw new InvalidOperationException("前摇身份或恢复边界不匹配。");

            foreach (var transaction in transactions.Transactions)
                if (!transaction.Handle.Equals(handle) || transaction.ActorId != actorId ||
                    transaction.SkillId != entry.SkillId || transaction.SkillSlot != entry.SkillSlot ||
                    transaction.ResourceType != (int)ResourceType.Mana || transaction.ChargeCost != 0 ||
                    transaction.CooldownGroupId != 0 || transaction.SharedCooldownMs != 0 || transaction.GlobalCooldownMs != 0)
                    throw new InvalidOperationException("前摇事务超出单槽法力和冷却的恢复范围。");

            foreach (var refund in transactions.KillRefunds)
                if (!refund.Handle.Equals(handle) || refund.ActorId != actorId || refund.SkillId != entry.SkillId ||
                    refund.ResourceType != (int)ResourceType.Mana || refund.AmountRaw != transactions.Transactions[0].ResourceAmountRaw)
                    throw new InvalidOperationException("退款记录不属于同一个施法事务。");

            ValidatePipeline(cast.PrepareRunnerState(runner));
            if (!cast.TryGetLatestRunning(actorId, out var active))
                throw new InvalidOperationException("前摇截面未找到对应的施法进度。");
            return new LocalStateSyncWindupCut(in stamp, in economy, runner, in runtime, in transactions,
                services.Resolve<MobaExecutionContextRegistry>(), firstSlotInitialized, secondSlotInitialized, active.ElapsedMs);

            LocalStateSyncCastSlot ReadSlot(int slot, out bool initialized)
            {
                var slots = actor.skillLoadout.ActiveSkills;
                if (slot <= 0 || slot > slots.Length || firstSlot == secondSlot)
                    throw new InvalidOperationException("前摇经济截面缺少有效的双槽装载。");
                var loadout = slots[slot - 1];
                if ((loadout.ChargesConfigured && (loadout.MaxCharges != 1 || loadout.CurrentCharges != 1 ||
                    loadout.ChargeRecoveryMs != 0 || loadout.NextChargeRecoveryTimeMs != 0)) ||
                    loadout.CooldownGroupId != 0 || loadout.IgnoreGlobalCooldown)
                    throw new InvalidOperationException("前摇经济截面不覆盖充能或共用冷却。");
                initialized = loadout.ChargesConfigured;
                return new LocalStateSyncCastSlot(slot, loadout.SkillId, loadout.Level,
                    loadout.CooldownEndTimeMs, loadout.CooldownDurationMs);
            }
        }

        internal void Restore(EntitasWorld candidate, in LocalStateSyncCastScope scope, int tick, ActorEntity actor)
        {
            if (_stamp.Tick != tick || !_stamp.Scope.Matches(in scope))
                throw new InvalidOperationException("前摇截面与候选代际或 Tick 不匹配。");

            var services = candidate.Services;
            var cast = services.Resolve<SkillCastCoordinator>();
            var prepared = cast.PrepareRunnerState(_runner);
            ValidatePipeline(prepared);
            var contexts = services.Resolve<MobaExecutionContextRegistry>();
            contexts.RestoreLocalRegistry(_nodes, _nextContextId);
            services.Resolve<MobaSkillCastRuntimeService>().RestoreRollbackSnapshot(in _runtime);
            RestoreSlotInitialization(Economy.First.Slot, _firstSlotInitialized);
            RestoreSlotInitialization(Economy.Second.Slot, _secondSlotInitialized);
            services.Resolve<MobaSkillEconomyService>().RestoreRollbackSnapshot(in _transactions);
            cast.ImportPreparedRunnerRollbackState(prepared, localRollback: true);

            void RestoreSlotInitialization(int slot, bool initialized)
            {
                if (!initialized) return;
                var loadout = actor.skillLoadout.ActiveSkills[slot - 1];
                loadout.ChargesConfigured = true;
                loadout.MaxCharges = loadout.CurrentCharges = 1;
                loadout.ChargeRecoveryMs = 0;
                loadout.NextChargeRecoveryTimeMs = 0;
            }
        }

        private static void ValidatePipeline(SkillCastCoordinator.PreparedRunnerRollback prepared)
        {
            var definition = prepared.Definitions[0];
            foreach (var phase in definition.PreCastPhases)
                if (!(phase is AbilityDelayPhase<SkillPipelineContext> || phase is SkillEconomyPhase || phase is SkillCommitPointPhase))
                    throw new InvalidOperationException("前摇配方包含未覆盖的阶段。");

            if (definition.CastPhases != null)
                foreach (var phase in definition.CastPhases)
                    if (!(phase is AbilityDelayPhase<SkillPipelineContext>))
                        throw new InvalidOperationException("前摇配方完成后不能进入未覆盖的战斗阶段。");
        }
    }

    internal readonly struct LocalStateSyncBattleCastObservation
    {
        internal readonly int Tick;
        internal readonly LocalStateSyncCastEconomy Economy;
        internal readonly Vec3 Position;
        internal readonly int? CastElapsedMs;
        internal readonly Quat? Rotation;
        internal readonly LocalStateSyncFlightObservation? Flight;

        internal LocalStateSyncBattleCastObservation(int tick, LocalStateSyncCastEconomy economy, Vec3 position,
            int? castElapsedMs = null, Quat? rotation = null, LocalStateSyncFlightObservation? flight = null)
        {
            Tick = tick;
            Economy = economy;
            Position = position;
            CastElapsedMs = castElapsedMs;
            Rotation = rotation;
            Flight = flight;
        }
    }

    /// <summary>
    /// 独占工厂提供的、已初始化的 Tick 0 正式战斗世界。
    /// 工厂只装配离线单施法者配方，不接传输、权威句柄或外部表现订阅者。
    /// </summary>
    internal sealed class LocalStateSyncBattleCastWorld :
        ILocalStateSyncCastWorld<LocalStateSyncBattleCastCut, LocalStateSyncBattleCastObservation>, ILocalStateSyncMovementWorld
    {
        private readonly LocalStateSyncCastScope _scope;
        private readonly float _delta;
        private readonly Func<EntitasWorld> _create;
        private readonly bool _observeCastProgress;
        private readonly bool _observeActorPose;
        private readonly int _projectileMask;
        private bool _restoreMovement;
        private EntitasWorld _world;
        private FrameTime _frame;
        private ActorEntity _actor;
        private MobaActorRegistry _actors;
        private SkillCastCoordinator _cast;
        private LocalStateSyncFlightObservation? _flight;
        private int _firstSlot, _secondSlot;

        internal LocalStateSyncBattleCastWorld(in LocalStateSyncCastScope scope, float delta, Func<EntitasWorld> create,
            bool observeCastProgress = false, int projectileMask = 0, bool observeActorPose = false)
        {
            if (!(delta > 0f) || float.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            _scope = scope;
            _delta = delta;
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _observeCastProgress = observeCastProgress;
            _observeActorPose = observeActorPose;
            _projectileMask = projectileMask;
        }

        /// <summary>
        /// 本地单角色配方在首次 Tick 前显式选择无碰撞求解器；不改正式默认装配。
        /// </summary>
        internal static void ConfigureIsolatedLocomotion(ActorEntity actor)
        {
            if (!actor.hasMotion || actor.motion.Initialized)
                throw new InvalidOperationException("隔离移动必须在首次初始化 Motion 前装配。");
            var motion = actor.motion;
            actor.ReplaceMotion(motion.Pipeline, motion.State, motion.Output, NoMotionSolver.Instance,
                motion.Policy, motion.Events, motion.Initialized, motion.HitTriggerRuntime);
        }

        public void Restore(LocalStateSyncBattleCastCut cut)
        {
            if (_world != null || cut.Tick < 0 || cut.Actor.ActorId != _scope.ActorId ||
                cut.Actor.OwnerNetId <= 0 || (ulong)cut.Actor.OwnerNetId != _scope.PlayerId ||
                cut.Actor.Kind != 1 || cut.Economy.First.Slot == cut.Economy.Second.Slot)
                throw new InvalidOperationException("候选截面不匹配施法者或两槽配置。");
            if (_projectileMask == 0 ? cut.Projectile != null : cut.Projectile == null || cut.Windup != null ||
                cut.Movement.HasValue || !cut.Projectile.MatchesScope(in _scope, cut.Tick, _projectileMask))
                throw new InvalidOperationException("候选缺少匹配的独立弹体截面。");

            _world = _create() ?? throw new InvalidOperationException("未装配离线战斗世界。");
            var services = _world.Services;
            _actors = services.Resolve<MobaActorRegistry>();
            _cast = services.Resolve<SkillCastCoordinator>();
            var economy = services.Resolve<MobaSkillEconomyService>();
            if (services.Resolve<IFrameTime>() is not FrameTime frame || frame.Frame.Value != 0 ||
                services.Resolve<IWorldClock>() is not WorldClock clock ||
                !_actors.MatchesEntityVersion(_scope.ActorId, _scope.EntityVersion) ||
                !_actors.TryGet(_scope.ActorId, out _actor) || _actors.CopyActorIdsInOrder().Count != 1 ||
                _cast.TryGetLatestRunning(_scope.ActorId, out _) || economy.PendingTransactionCount != 0)
                throw new InvalidOperationException("候选不是匹配的空闲单施法者战斗世界。");

            var first = ValidateSlot(cut.Economy.First);
            var second = ValidateSlot(cut.Economy.Second);
            var mana = _actor.resourceContainer.Value.Map[ResourceType.Mana];
            if (cut.Economy.ManaRaw < 0 || cut.Economy.ManaRaw > mana.LastMax.RawValue)
                throw new InvalidOperationException("候选法力不在可恢复范围内。");

            var importer = new MobaLogicWorldStateImporter(_actors, null, null);
            var result = importer.ImportScopedFullSnapshot(new[] { cut.Actor }, cut.Tick, new[] { _scope.ActorId });
            if (result.Updated != 1 || result.Failed != 0 || !frame.AlignTo(new FrameIndex(cut.Tick), _delta))
                throw new InvalidOperationException("角色快照或候选时钟恢复失败。");

            mana.Current = Fixed64.FromRaw(cut.Economy.ManaRaw);
            MobaResourceAttributeContextProjector.RefreshRestored(_actor);
            RestoreCooldown(first, cut.Economy.First);
            RestoreCooldown(second, cut.Economy.Second);
            clock.RestoreState(frame.Time, _delta);
            cut.Windup?.Restore(_world, in _scope, cut.Tick, _actor);
            cut.Projectile?.Restore(_world, _actor);
            _flight = cut.Projectile?.Observation;
            if (cut.Movement is { } movement)
            {
                if (!LocalStateSyncMovementCut.IsFreshIsolatedMotion(_actor) ||
                    !TryReadMovement(out var current) || !current.CanMove || current.MoveSpeed != movement.State.MoveSpeed)
                    throw new InvalidOperationException("候选的普通移动装配或有效属性与权威截面不符。");
                var pose = movement.State.Pose;
                _actor.ReplaceTransform(pose);
                MobaMotionStateSync.SyncFromTransform(_actor, in pose);
                if (movement.HasMoveInput)
                    _actor.ReplaceMoveInput(movement.State.MoveX, movement.State.MoveZ);
                else if (_actor.hasMoveInput)
                    _actor.RemoveMoveInput();
                _restoreMovement = true;
            }
            _frame = frame;
            _firstSlot = cut.Economy.First.Slot;
            _secondSlot = cut.Economy.Second.Slot;
        }

        public bool TryStep(int tick, SkillInputEvent? input, out string rejection) =>
            Step(tick, input, null, out rejection);

        public bool TryStepMovement(int tick, in MobaActorIntent intent, out string rejection) =>
            Step(tick, null, intent, out rejection);

        private bool Step(int tick, SkillInputEvent? input, MobaActorIntent? move, out string rejection)
        {
            rejection = null;
            if (_frame == null || tick != _frame.Frame.Value + 1 || (move.HasValue && !_restoreMovement) ||
                !_actors.MatchesEntityVersion(_scope.ActorId, _scope.EntityVersion)) return false;
            if (input is { } command && (command.TargetFrame != tick || command.Phase != SkillInputPhase.Press ||
                (command.Slot != _firstSlot && command.Slot != _secondSlot) ||
                (command.TargetActorId != 0 && command.TargetActorId != _scope.ActorId))) return false;

            _frame.StepTo(new FrameIndex(tick), _delta);
            if (move is { } direction && !MobaMoveInputCommandHandler.TryApplyDirection(_actor, _scope.ActorId, _cast, in direction))
                return false;
            if (input is { } press)
            {
                var result = _cast.TryHandleInputResult(_scope.ActorId, in press);
                if (!result.Success) rejection = result.Code;
            }
            _world.Tick(_delta);
            if (_projectileMask != 0)
            {
                var flight = LocalStateSyncProjectileCut.ReadFlight(_world, _scope.ActorId, _projectileMask, _actor, out var projectile);
                if (projectile == null) _flight = null;
                else
                {
                    var actorId = projectile.actorId.Value;
                    _flight = new LocalStateSyncFlightObservation(flight, actorId, _actors.GetEntityVersion(actorId), projectile.transform.Value);
                }
            }
            return _actors.MatchesEntityVersion(_scope.ActorId, _scope.EntityVersion) &&
                _actors.TryGet(_scope.ActorId, out _) && (_projectileMask != 0 || _actors.CopyActorIdsInOrder().Count == 1) &&
                (!_restoreMovement || (TryReadMovement(out var movement) && movement.ActorId == _scope.ActorId &&
                    movement.EntityVersion == _scope.EntityVersion && movement.HasIsolatedLocomotion));
        }

        private bool TryReadMovement(out MobaAuthoritativeMovementAnchor movement) =>
            LocalStateSyncMovementCut.TryRead(_world.Services, in _scope, out movement);

        public LocalStateSyncBattleCastObservation Read() => new LocalStateSyncBattleCastObservation(_frame.Frame.Value,
            new LocalStateSyncCastEconomy(_actor.resourceContainer.Value.Map[ResourceType.Mana].Current.RawValue,
                ReadSlot(_firstSlot), ReadSlot(_secondSlot)), _actor.transform.Value.Position,
            _observeCastProgress && _cast.TryGetLatestRunning(_scope.ActorId, out var active) ? active.ElapsedMs : (int?)null,
            _restoreMovement || _observeActorPose ? _actor.transform.Value.Rotation : (Quat?)null, _flight);

        private ActiveSkillRuntime ValidateSlot(LocalStateSyncCastSlot state)
        {
            var loadout = _actor.skillLoadout.ActiveSkills;
            if (state.Slot <= 0 || state.Slot > loadout.Length || state.SkillId <= 0 || state.EndMs < 0 || state.DurationMs < 0)
                throw new InvalidOperationException("候选槽冷却截面无效。");
            var slot = loadout[state.Slot - 1];
            if (slot.SkillId != state.SkillId || slot.Level != state.Level || slot.ChargesConfigured || slot.CooldownGroupId != 0)
                throw new InvalidOperationException("候选槽装载身份变化或经济状态超出离线范围。");
            return slot;
        }

        private static void RestoreCooldown(ActiveSkillRuntime slot, LocalStateSyncCastSlot state)
        {
            slot.CooldownEndTimeMs = state.EndMs;
            slot.CooldownDurationMs = state.DurationMs;
        }

        private LocalStateSyncCastSlot ReadSlot(int number)
        {
            var slot = _actor.skillLoadout.ActiveSkills[number - 1];
            return new LocalStateSyncCastSlot(number, slot.SkillId, slot.Level, slot.CooldownEndTimeMs, slot.CooldownDurationMs);
        }

        public void Dispose()
        {
            var world = _world;
            _world = null;
            _frame = null;
            world?.Dispose();
        }
    }
}
