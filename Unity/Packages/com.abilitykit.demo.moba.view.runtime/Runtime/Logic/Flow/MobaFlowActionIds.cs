namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 流程动作 / 切换流的字符串标识常量。
    /// 纯数据、零依赖，供 <see cref="MobaFlowConfiguration"/> 装配 feature spec、
    /// 供 <c>MobaFlowActionExecutor</c> / <c>MobaFlowSwitchExecutor</c> 做字符串派发。
    /// 从 <c>MobaFlowActions.cs</c> 拆出，使其可独立镜像编译进桌面测试程序集。
    /// </summary>
    internal static class MobaFlowActionIds
    {
        public const string ResetBattleSessionRuntimeState = "battle.reset_session_runtime_state";
        public const string ReturnLobbyAfterBattleEnd = "battle.return_lobby_after_end";
    }

    /// <summary>
    /// 切换流（switch flow）字符串标识常量。语义同 <see cref="MobaFlowActionIds"/>。
    /// </summary>
    internal static class MobaFlowSwitchIds
    {
        public const string AdvanceOnConnectEnter = "battle.advance_on_connect_enter";
        public const string AdvanceOnCreateOrJoinWorldEnter = "battle.advance_on_create_or_join_world_enter";
        public const string AdvanceOnLoadAssetsEnter = "battle.advance_on_load_assets_enter";
    }
}
