using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public readonly struct BattleSessionModuleContext
    {
        public readonly GamePhaseContext Phase;
        public readonly BattleSessionFeature Feature;

        public BattleEventBus Events { get; }
        public BattleSessionHooks Hooks { get; }

        public BattleSessionModuleContext(in GamePhaseContext phase, BattleSessionFeature feature)
        {
            Phase = phase;
            Feature = feature;
            Events = feature != null ? feature.Events : null;
            Hooks = feature != null ? feature.Hooks : null;
        }

        public BattleLogicSession Session => Feature.Session;
        public BattleStartPlan Plan => Feature.Plan;
        public int LastFrame => Feature.LastFrame;
    }
}
