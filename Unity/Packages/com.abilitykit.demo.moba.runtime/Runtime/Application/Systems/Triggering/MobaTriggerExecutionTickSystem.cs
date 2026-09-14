using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services.Triggering;

namespace AbilityKit.Demo.Moba.Systems.Triggering
{
    [WorldSystem(order: MobaSystemOrder.TriggerExecutionTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaTriggerExecutionTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaTriggerExecutionRuntimeService _runtime;

        public MobaTriggerExecutionTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _runtime);
        }

        protected override void OnExecute()
        {
            if (_clock == null || _runtime == null || _clock.DeltaTime <= 0f) return;
            _runtime.Tick(_clock.DeltaTime * 1000f);
        }
    }
}
