namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Flow 编排层向表现层推送事件的接口（Logic → View 方向）。
    /// 灵感来自 ET 的 IETViewEventSink，但按关注点分离为精简成员。
    /// 与 <see cref="IFlowCommandSink"/>（View → Logic 命令方向）互补，构成双向通信契约。
    /// </summary>
    /// <remarks>
    /// 实现方（Unity 宿主 / 测试 mock）订阅这些事件来驱动 UI 状态切换、场景加载等表现行为。
    /// Flow 层只依赖此接口，完全不知道 View 的存在。
    /// </remarks>
    public interface IPresentationSink
    {
        /// <summary>
        /// Root / Battle 阶段发生变化时调用。
        /// </summary>
        /// <param name="root">当前 Root 阶段。</param>
        /// <param name="battle">当前 Battle 子阶段（Root 非 Battle 时为默认值）。</param>
        void OnPhaseChanged(MobaRootState root, MobaBattleState battle);

        /// <summary>
        /// 战斗正式开始（进入 InMatch）时调用。
        /// </summary>
        void OnBattleStart();

        /// <summary>
        /// 战斗结束（进入 End 或退出 Battle）时调用。
        /// </summary>
        void OnBattleEnd();

        /// <summary>
        /// 流程编排层遇到错误时调用。
        /// </summary>
        /// <param name="message">错误描述。</param>
        void OnError(string message);
    }
}
