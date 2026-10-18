namespace AbilityKit.Game.Flow
{
    public readonly struct MobaFlowStateSnapshot
    {
        public MobaFlowStateSnapshot(MobaRootState rootPhase, MobaBattleState? battlePhase,
            bool isExiting, bool isFaulted)
        {
            RootPhase = rootPhase;
            BattlePhase = battlePhase;
            IsExiting = isExiting;
            IsFaulted = isFaulted;
        }

        public MobaRootState RootPhase { get; }
        public MobaBattleState? BattlePhase { get; }
        public bool IsExiting { get; }
        public bool IsFaulted { get; }
    }

    /// <summary>
    /// 表现层向流程编排层提交命令的接口。
    /// Feature / View 通过此接口请求流程变更，不直接引用流程编排具体类型。
    /// </summary>
    public interface IFlowCommandSink
    {
        MobaFlowStateSnapshot CurrentState { get; }
        /// <summary>当前 Root 阶段状态。</summary>
        MobaRootState CurrentRootPhase { get; }

        /// <summary>当前 Battle 子阶段状态。</summary>
        MobaBattleState CurrentBattlePhase { get; }

        /// <summary>请求进入战斗（不带 bootstrapper）。</summary>
        void RequestEnterBattle();

        /// <summary>请求结束当前战斗。</summary>
        void RequestBattleEnd();

        /// <summary>请求返回大厅。</summary>
        void RequestReturnLobby();
    }
}
