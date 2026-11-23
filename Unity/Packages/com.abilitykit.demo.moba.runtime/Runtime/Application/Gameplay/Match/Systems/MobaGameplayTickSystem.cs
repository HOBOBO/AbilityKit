using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Gameplay.Systems
{
    [WorldSystem(order: MobaSystemOrder.GameplayTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaGameplayTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaLogicWorldRunGateService _phase;
        private MobaGameplayService _gameplay;

        public MobaGameplayTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _phase);
            Services.TryResolve(out _gameplay);
        }

        protected override void OnExecute()
        {
            if (_clock == null || _gameplay == null) return;
            if (_phase != null && !_phase.InGame) return;

            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            _gameplay.Tick(dt);
        }
    }
}
