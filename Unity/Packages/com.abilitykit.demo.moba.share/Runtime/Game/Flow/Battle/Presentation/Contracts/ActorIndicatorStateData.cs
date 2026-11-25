namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 平台无关的完整指示器条目；Availability 为 0 不可用、1 有效、2 条件隐藏。
    /// </summary>
    public readonly struct ActorIndicatorStateData
    {
        public readonly int ActorId, LayoutId, IndicatorId, Availability;
        public readonly float Current, Max;
        public readonly bool HasMax, Ready;

        /// <summary>
        /// 由经过验证的协议值构建只读显示状态。
        /// </summary>
        public ActorIndicatorStateData(int actorId, int layoutId, int indicatorId, float current, float max,
            bool hasMax, bool ready, int availability)
        { ActorId = actorId; LayoutId = layoutId; IndicatorId = indicatorId; Current = current;
          Max = max; HasMax = hasMax; Ready = ready; Availability = availability; }
    }
}
