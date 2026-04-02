namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// blink Action 的强类型参数
    /// </summary>
    public readonly struct BlinkArgs
    {
        /// <summary>
        /// 闪烁距离（单位）
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// 闪烁方向模式
        /// 0=朝技能瞄准方向, 1=朝目标方向
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 优先级
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 是否应用到释放者（默认 caster）
        /// </summary>
        public readonly bool ApplyToCaster;

        public BlinkArgs(float distance, int directionMode = 0, int priority = 15, bool applyToCaster = true)
        {
            Distance = distance;
            DirectionMode = directionMode;
            Priority = priority;
            ApplyToCaster = applyToCaster;
        }

        public static BlinkArgs Default => new BlinkArgs(0f, 0, 15, true);
    }
}
