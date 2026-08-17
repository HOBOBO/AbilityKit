using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Effect;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Protocol.Moba;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public sealed class BattleViewEventSink : IBattleViewEventSink
    {
        private readonly BattleAreaViewEventHandler _areaEvents;
        private readonly BattleDamageViewEventHandler _damageEvents;
        private readonly BattleProjectileViewEventHandler _projectileEvents;
        private readonly BattleSummonViewEventHandler _summonEvents;
        private readonly BattleActorDeathViewEventHandler _deathEvents;
        private readonly BattleActorRespawnViewEventHandler _respawnEvents;
        private readonly BattlePresentationCueViewEventHandler _presentationCues;
        private readonly BattleViewDirtyEntityRefresher _dirtyViews;
        private readonly bool _presentDamageTriggers;
        private readonly bool _presentDamageSnapshots;

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

            _areaEvents = handlers.CreateAreaEvents(ctx, query, binder, areaViews);
            _damageEvents = handlers.CreateDamageEvents(ctx, query, in vfxNode, floatingTexts);
            _projectileEvents = handlers.CreateProjectileEvents(ctx, query, vfx, in vfxNode, resources, hierarchy);
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
        }

        public void OnTriggerEvent(in TriggerEvent evt)
        {
            if (evt.Id == null) return;

            if (evt.Id == DamagePipelineEvents.AfterApply)
            {
                if (_presentDamageTriggers && evt.Payload is DamageResult result)
                {
                    _damageEvents.HandleDamageResult(result);
                }
                return;
            }

            if (evt.Id == ProjectileTriggering.Events.Hit)
            {
                _projectileEvents.HandleTriggerHit(evt);
            }
        }

        public void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload)
        {
            _summonEvents?.Handle(eventId, in payload);
        }

        public void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res)
        {
            _dirtyViews.Refresh();
        }

        public void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries)
        {
            _dirtyViews.Refresh();
        }

        public void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotEntry[] entries)
        {
            _projectileEvents.HandleSnapshot(entries);
        }

        public void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotEntry[] entries)
        {
            _areaEvents.HandleSnapshot(entries);
        }

        public void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotEntry[] entries)
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
            _projectileEvents?.Tick();
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
