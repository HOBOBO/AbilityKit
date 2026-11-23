using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Gameplay.Systems
{
    [WorldSystem(order: MobaSystemOrder.MinionWaveTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaMinionWaveTickSystem : WorldSystemBase
    {
        private MobaMinionWaveService _minions;
        private MobaMinionCombatService _combat;
        private MobaStructureService _structures;
        private MobaLogicWorldRunGateService _phase;
        private IMobaGameplayFeatureSet _features;

        public MobaMinionWaveTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services) { }

        protected override void OnInit()
        {
            Services.TryResolve(out _minions);
            Services.TryResolve(out _combat);
            Services.TryResolve(out _structures);
            Services.TryResolve(out _phase);
            Services.TryResolve(out _features);
        }

        protected override void OnExecute()
        {
            if (_phase != null && !_phase.InGame)
            {
                _minions?.Reset();
                _combat?.Reset();
                _structures?.Reset();
                return;
            }

            // Optional lane gameplay is opt-in. A world that has not completed
            // gameplay feature configuration must not start spawning minions or
            // structures while bootstrap is still settling.
            if (_features == null || !_features.IsConfigured)
            {
                _minions?.Reset();
                _combat?.Reset();
                _structures?.Reset();
                return;
            }

            if (_features.IsEnabled(MobaGameplayFeatureIds.Structures))
                _structures?.Tick();
            else
                _structures?.Reset();

            if (_features.IsEnabled(MobaGameplayFeatureIds.MinionWaves))
            {
                _minions?.Tick();
                _combat?.Tick();
            }
            else
            {
                _minions?.Reset();
                _combat?.Reset();
            }
        }
    }
}
