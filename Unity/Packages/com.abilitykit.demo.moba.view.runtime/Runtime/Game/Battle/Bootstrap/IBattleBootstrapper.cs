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

    /// <summary>
    /// 副本提供者。实现方只需给出副本 id，其余（玩法、地图、流程 Profile、场景、胜负规则）
    /// 都由副本定义解析，新增副本不需要改动 bootstrapper。
    /// </summary>
    public interface IBattleDungeonProvider
    {
        int DungeonId { get; }
    }

    /// <summary>
    /// 本局玩家数来源。副本入场校验用它检查人数是否落在副本定义允许的区间内。
    /// </summary>
    public interface IBattlePlayerCountProvider
    {
        int PlayerCount { get; }
    }
}
