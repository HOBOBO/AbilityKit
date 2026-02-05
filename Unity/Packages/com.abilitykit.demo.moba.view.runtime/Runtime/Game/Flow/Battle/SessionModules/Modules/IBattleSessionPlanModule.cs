namespace AbilityKit.Game.Flow.Battle.Modules
{
    public interface IBattleSessionPlanModule
    {
        bool OnPlanBuilt(in BattleSessionModuleContext ctx);
    }
}
