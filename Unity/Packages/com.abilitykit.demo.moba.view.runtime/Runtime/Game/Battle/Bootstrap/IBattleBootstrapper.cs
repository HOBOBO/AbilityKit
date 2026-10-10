namespace AbilityKit.Game.Flow
{
    public interface IBattleBootstrapper
    {
        BattleStartPlan Build();
    }

    public interface IBattleFlowProfileProvider
    {
        string FlowProfileId { get; }
    }

    public interface IBattleFlowModeProvider
    {
        string FlowHostMode { get; }
    }
}
