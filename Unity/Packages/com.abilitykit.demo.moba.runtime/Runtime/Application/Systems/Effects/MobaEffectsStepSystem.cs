using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Effect;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems.Effects
{
    /// <summary>
    /// 效果步骤系统
    /// 执行所有单位的 Effects.Step()
    /// </summary>
    [WorldSystem(order: MobaSystemOrder.EffectsStep)]
    public sealed class MobaEffectsStepSystem : WorldSystemBase
    {
        private IFrameTime _time;
        private IEventBus _eventBus;
        private IUnitResolver _units;

        private MobaWorldSystemServices _systemServices;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaEffectsStepSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _units);
            Services.TryResolve(out _time);
            Services.TryResolve(out _eventBus);
            _systemServices = MobaWorldSystemExecution.Resolve(Services);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId));
        }

        protected override void OnExecute()
        {
            MobaWorldSystemExecution.Require(
                _units != null && _time != null && _group != null,
                Services,
                nameof(MobaEffectsStepSystem),
                "effects.step.dependencies",
                "IUnitResolver, IFrameTime and actor group",
                $"hasUnits={_units != null}, hasFrameTime={_time != null}, hasGroup={_group != null}");

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            var start = _systemServices.StartTimestamp;
            var processed = 0;
            var sp = new WorldServiceProviderAdapter(Services);

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId) continue;

                var actorId = e.actorId.Value;
                if (!_units.TryResolve(new EcsEntityId(actorId), out var unit) || unit == null) continue;

                var ctx = new EffectExecutionContext(
                    services: sp,
                    time: _time,
                    source: unit,
                    target: unit,
                    targetUnit: unit,
                    eventBus: _eventBus,
                    sourceContextId: 0
                );

                try
                {
                    unit.Effects.Step(in ctx);
                    processed++;
                }
                catch (Exception ex)
                {
                    MobaWorldSystemExecution.HandleException(
                        in _systemServices,
                        ex,
                        nameof(MobaEffectsStepSystem),
                        "effects.step",
                        actorId: actorId);
                }
            }

            MobaWorldSystemExecution.Sample(in _systemServices, "moba.effects.actorCandidates", entities.Length);
            MobaWorldSystemExecution.Sample(in _systemServices, "moba.effects.processed", processed);
            MobaWorldSystemExecution.RecordDuration(
                in _systemServices,
                MobaBattleDiagnosticMetric.EffectsStep,
                start,
                MobaBattleDiagnosticsDefaults.EffectsStepWarnMs);
        }
    }
}
