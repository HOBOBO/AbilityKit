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
        private MobaLogicWorldRunGateService _phase;
        private IMobaGameplayFeatureSet _features;

        public MobaMinionWaveTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services) { }

        protected override void OnInit()
        {
            Services.TryResolve(out _minions);
            Services.TryResolve(out _combat);
            Services.TryResolve(out _phase);
            Services.TryResolve(out _features);
        }

        protected override void OnExecute()
        {
            if (_phase != null && !_phase.InGame)
            {
                _minions?.Reset();
                _combat?.Reset();
                return;
            }

            if (_features != null && _features.IsConfigured &&
                !_features.IsEnabled(MobaGameplayFeatureIds.MinionWaves))
            {
                _minions?.Reset();
                _combat?.Reset();
                return;
            }

            if (_minions == null) return;
            _minions.Tick();
            _combat?.Tick();
        }
    }
}
