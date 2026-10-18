namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 将 <c>MobaFlowActionExecutor</c> 和 <c>MobaFlowSwitchExecutor</c>
    /// 与具体 flow domain 解耦，使派发逻辑可以独立测试。
    /// </summary>
    internal interface IMobaFlowActionTarget
    {
        void ResetBattleSessionRuntimeState();
        void ReturnLobbyAfterBattleEnd();

        void TryAdvanceOnConnectEnter();
        void TryAdvanceOnCreateOrJoinWorldEnter();
        void TryAdvanceOnLoadAssetsEnter();
    }
}
