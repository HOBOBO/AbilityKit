using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Gameplay.Systems
{
    [WorldSystem(order: MobaSystemOrder.GameplayRespawnTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaGameplayRespawnTickSystem : WorldSystemBase
    {
        private IWorldClock _clock;
        private MobaLogicWorldRunGateService _phase;
        private MobaGameplayRespawnService _respawns;

        public MobaGameplayRespawnTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services) { }

        protected override void OnInit()
        {
            Services.TryResolve(out _clock);
            Services.TryResolve(out _phase);
            Services.TryResolve(out _respawns);
        }

        protected override void OnExecute()
        {
            if (_clock == null || _respawns == null || _clock.DeltaTime <= 0f) return;
            if (_phase != null && !_phase.InGame) return;
            _respawns.Tick();
        }
    }
}
