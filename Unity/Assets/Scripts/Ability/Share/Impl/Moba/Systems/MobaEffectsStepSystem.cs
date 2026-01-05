using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.EffectsStep)]
    public sealed class MobaEffectsStepSystem : WorldSystemBase
    {
        private IFrameTime _time;
        private IEventBus _eventBus;
        private IUnitResolver _units;

        private Entitas.IGroup<global::ActorEntity> _group;

        public MobaEffectsStepSystem(global::Contexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _units);
            Services.TryGet(out _time);
            Services.TryGet(out _eventBus);
            _group = Contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId));
        }

        protected override void OnExecute()
        {
            if (_units == null || _time == null) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

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
                    eventBus: _eventBus
                );

                unit.Effects.Step(in ctx);
            }
        }
    }
}
