namespace AbilityKit.Game.Flow.Battle.Modules
{
    public interface IBattleSessionPreTickModule
    {
        void PreTick(in BattleSessionModuleContext ctx, float deltaTime);
    }
}
