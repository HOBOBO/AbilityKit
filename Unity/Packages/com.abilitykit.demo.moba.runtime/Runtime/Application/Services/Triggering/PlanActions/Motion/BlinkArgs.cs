namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// blink Action 的强类型参数。
    /// </summary>
    public readonly struct BlinkArgs
    {
        /// <summary>
        /// 闪烁距离，单位为逻辑距离。
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// 闪烁方向模式：0=朝技能瞄准方向，1=朝目标方向。
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 运动优先级。
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 是否应用到释放者，默认 caster。
        /// </summary>
        public readonly bool ApplyToCaster;

        /// <summary>
        /// 是否可穿墙：true 时行进忽略墙体，但终点落障碍物内会沿方向投影到边界。
        /// </summary>
        public readonly bool PassThroughWalls;

        public BlinkArgs(float distance, int directionMode = 0, int priority = 15, bool applyToCaster = true, bool passThroughWalls = false)
        {
            Distance = distance;
            DirectionMode = directionMode;
            Priority = priority;
            ApplyToCaster = applyToCaster;
            PassThroughWalls = passThroughWalls;
        }

        public static BlinkArgs Default => new BlinkArgs(0f, 0, 15, true, false);
    }
}
