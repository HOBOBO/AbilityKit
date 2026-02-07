using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public interface IBattleSessionModuleHost
    {
        BattleEventBus Events { get; }
        BattleSessionHooks Hooks { get; }

        BattleLogicSession Session { get; }
        BattleStartPlan Plan { get; }
        int LastFrame { get; }
    }

    public readonly struct BattleSessionModuleContext
    {
        public readonly GamePhaseContext Phase;
        public readonly IBattleSessionModuleHost Host;

        public BattleEventBus Events { get; }
        public BattleSessionHooks Hooks { get; }

        public BattleSessionModuleContext(in GamePhaseContext phase, IBattleSessionModuleHost host)
        {
            Phase = phase;
            Host = host;
            Events = host != null ? host.Events : null;
            Hooks = host != null ? host.Hooks : null;
        }

        public BattleLogicSession Session => Host != null ? Host.Session : null;
        public BattleStartPlan Plan => Host != null ? Host.Plan : default;
        public int LastFrame => Host != null ? Host.LastFrame : 0;
    }
}
