namespace AbilityKit.Game.Flow.Battle.Modules
{
    public interface IBattleSessionLifecycleModule
    {
        void OnSessionStarting(in BattleSessionModuleContext ctx);
        void OnSessionStopping(in BattleSessionModuleContext ctx);
    }
}
