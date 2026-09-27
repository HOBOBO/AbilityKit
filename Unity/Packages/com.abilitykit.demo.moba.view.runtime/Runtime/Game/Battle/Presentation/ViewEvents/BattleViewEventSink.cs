using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Effect;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Share;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public sealed class BattleViewEventSink : IBattleViewEventSink
    {
        private readonly BattleContext _context;
        private readonly BattleAreaViewEventHandler _areaEvents;
        private readonly BattleDamageViewEventHandler _damageEvents;
        private readonly BattleProjectileViewEventHandler _projectileEvents;
        private readonly BattleViewPoolStatsOverlay _poolStatsOverlay;
        private readonly BattleProjectilePoolStatsProvider _projectileStatsProvider;
        private readonly BattleSummonViewEventHandler _summonEvents;
        private readonly BattleActorDeathViewEventHandler _deathEvents;
        private readonly BattleActorRespawnViewEventHandler _respawnEvents;
        private readonly BattlePresentationCueViewEventHandler _presentationCues;
        private readonly BattleViewDirtyEntityRefresher _dirtyViews;
        private readonly bool _presentDamageTriggers;
        private readonly bool _presentDamageSnapshots;
        private long _observedRollbackCount;

        public BattleViewEventSink(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            BattleVfxManager vfx,
            EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts,
            BattleAreaViewSystem areaViews,
            BattleViewResourceProvider resources = null)
            : this(ctx, query, binder, vfx, in vfxNode, floatingTexts, areaViews, resources, null, null)
        {
        }

        internal BattleViewEventSink(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts,
            BattleAreaViewSystem areaViews,
            BattleViewResourceProvider resources,
            BattleViewEventSinkHandlerFactory handlers,
            BattleViewHierarchyManager hierarchy)
        {
            handlers ??= new BattleViewEventSinkHandlerFactory();

            _context = ctx;
            _areaEvents = handlers.CreateAreaEvents(ctx, query, binder, areaViews);
            _damageEvents = handlers.CreateDamageEvents(ctx, query, in vfxNode, floatingTexts);
            _projectileEvents = handlers.CreateProjectileEvents(ctx, query, vfx, in vfxNode, resources, hierarchy);
            _poolStatsOverlay = hierarchy?.Root != null
                ? hierarchy.Root.GetComponent<BattleViewPoolStatsOverlay>()
                : null;
            if (_poolStatsOverlay != null && _projectileEvents?.PoolForStats != null)
            {
                _projectileStatsProvider = new BattleProjectilePoolStatsProvider(_projectileEvents.PoolForStats);
                _poolStatsOverlay.RegisterProvider(_projectileStatsProvider);
            }
            _summonEvents = handlers.CreateSummonEvents(query, vfx, in vfxNode);
            _deathEvents = handlers.CreateDeathEvents(query, vfx, in vfxNode);
            _respawnEvents = handlers.CreateRespawnEvents(query, vfx, in vfxNode);
            _presentationCues = handlers.CreatePresentationCues(ctx, query, vfx, in vfxNode);
            _dirtyViews = handlers.CreateDirtyViews(ctx, ctx, query, binder);
            var sourceMode = ctx != null
                ? ctx.Plan.Sync.ViewEventSourceMode
                : BattleViewEventSourceMode.SnapshotOnly;
            _presentDamageTriggers = BattleDamagePresentationSourcePolicy.ShouldPresentTrigger(sourceMode);
            _presentDamageSnapshots = BattleDamagePresentationSourcePolicy.ShouldPresentSnapshot(sourceMode);
            _observedRollbackCount = ResolveRollbackCount();
        }

        public void OnDamageResult(in DamageResult result)
        {
            if (_presentDamageTriggers)
            {
                _damageEvents.HandleDamageResult(result);
            }
        }

        public void OnProjectileHit(in ProjectileHitEvent evt)
        {
            _projectileEvents.HandleTriggerHit(in evt);
        }

        public void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload)
        {
            _summonEvents?.Handle(eventId, in payload);
        }

        public void OnEnterGameSnapshot(ISnapshotEnvelope packet, BattleEnterGameSnapshot res)
        {
            _dirtyViews.Refresh();
        }

        public void OnActorTransformSnapshot(ISnapshotEnvelope packet, ActorTransformData[] entries)
        {
            _dirtyViews.Refresh();
        }

        public void OnProjectileEventSnapshot(ISnapshotEnvelope packet, ProjectileEventData[] entries)
        {
            _projectileEvents.HandleSnapshot(entries);
        }

        public void OnAreaEventSnapshot(ISnapshotEnvelope packet, AreaEventData[] entries)
        {
            _areaEvents.HandleSnapshot(entries);
        }

        public void OnDamageEventSnapshot(ISnapshotEnvelope packet, DamageEventData[] entries)
        {
            if (_presentDamageSnapshots)
            {
                _damageEvents.HandleSnapshot(entries);
            }
        }

        public void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries)
        {
            _presentationCues.HandleSnapshot(entries);
        }

        /// <summary>
        /// Per-frame tick for active projectile shell position updates.
        /// </summary>
        public void Tick()
        {
            ObservePresentationRollbackGeneration();
            _projectileEvents?.Tick();
        }

        /// <summary>
        /// Releases all transient state owned by event handlers. Safe to call repeatedly.
        /// </summary>
        public void Clear()
        {
            _poolStatsOverlay?.UnregisterProvider(_projectileStatsProvider);
            _projectileEvents?.Clear();
            _projectileEvents?.ClearPool();
            _presentationCues?.Clear();
            _observedRollbackCount = ResolveRollbackCount();
        }

        private void ObservePresentationRollbackGeneration()
        {
            var rollbackCount = ResolveRollbackCount();
            if (rollbackCount == _observedRollbackCount) return;

            _observedRollbackCount = rollbackCount;
            _presentationCues?.BeginReconciliationGeneration(
                _presentationCues.ReconciliationGeneration + 1);
        }

        private long ResolveRollbackCount()
        {
            var count = _context?.PredictionStats?.TotalRollbackCount ?? 0L;
            return count > 0L ? count : 0L;
        }
    }

    /// <summary>
    /// Subset of <see cref="AbilityKit.Demo.Moba.Events.Summon.SummonEventPayload"/>
    /// that is accessible from the view layer without a direct dependency on the runtime assembly.
    /// </summary>
    public readonly struct DemoMobaSummonEventPayload
    {
        public readonly int SummonActorId;
        public readonly int SummonId;
        public readonly int OwnerActorId;
        public readonly int RootOwnerActorId;
        public readonly int Reason;

        public DemoMobaSummonEventPayload(int summonActorId, int summonId, int ownerActorId, int rootOwnerActorId, int reason)
        {
            SummonActorId = summonActorId;
            SummonId = summonId;
            OwnerActorId = ownerActorId;
            RootOwnerActorId = rootOwnerActorId;
            Reason = reason;
        }
    }

    internal sealed class BattleViewEventSinkHandlerFactory
    {
        public BattleAreaViewEventHandler CreateAreaEvents(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            BattleAreaViewSystem areaViews)
        {
            return new BattleAreaViewEventHandler(ctx?.EntityWorld, query, binder, areaViews);
        }

        public BattleDamageViewEventHandler CreateDamageEvents(
            BattleContext ctx,
            IBattleEntityQuery query,
            in EC.IEntity vfxNode,
            BattleFloatingTextSystem floatingTexts)
        {
            return new BattleDamageViewEventHandler(ctx?.EntityWorld, query, in vfxNode, floatingTexts);
        }

        public BattleProjectileViewEventHandler CreateProjectileEvents(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleViewResourceProvider resources,
            BattleViewHierarchyManager hierarchy = null)
        {
            var shellPool = new BattleProjectileShellPool(
                factory: templateId => resources?.CreateProjectileShell(actorId: 0, projectileTemplateId: templateId),
                capacityPerTemplate: 8,
                hierarchy: hierarchy);
            return new BattleProjectileViewEventHandler(ctx?.EntityWorld, query, vfx, in vfxNode, resources, shellPool, null, hierarchy);
        }

        public BattleSummonViewEventHandler CreateSummonEvents(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattleSummonViewEventHandler(query, vfx, in vfxNode);
        }

        public BattleActorDeathViewEventHandler CreateDeathEvents(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattleActorDeathViewEventHandler(query, vfx, in vfxNode);
        }

        public BattleActorRespawnViewEventHandler CreateRespawnEvents(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattleActorRespawnViewEventHandler(query, vfx, in vfxNode);
        }

        public BattlePresentationCueViewEventHandler CreatePresentationCues(
            BattleContext ctx,
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            return new BattlePresentationCueViewEventHandler(ctx?.EntityWorld, query, vfx, in vfxNode);
        }

        public BattleViewDirtyEntityRefresher CreateDirtyViews(
            IBattleRuntimeContext runtimeContext,
            IBattleEntityContext entityContext,
            IBattleEntityQuery query,
            BattleViewBinder binder)
        {
            return new BattleViewDirtyEntityRefresher(
                runtimeContext,
                entityContext,
                query,
                binder,
                operation: null);
        }
    }
}
