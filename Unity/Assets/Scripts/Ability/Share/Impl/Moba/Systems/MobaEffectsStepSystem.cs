using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public sealed class MobaEffectsStepSystem : global::Entitas.IExecuteSystem
    {
        private readonly IWorldServices _services;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;

        private readonly global::Contexts _contexts;
        private readonly Entitas.IGroup<global::ActorEntity> _group;

        public MobaEffectsStepSystem(global::Contexts contexts, IWorldServices services, IFrameTime time, IEventBus eventBus, IUnitResolver units)
        {
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));

            _group = _contexts.actor.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId));
        }

        public void Execute()
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            var sp = new WorldServiceProviderAdapter(_services);

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
