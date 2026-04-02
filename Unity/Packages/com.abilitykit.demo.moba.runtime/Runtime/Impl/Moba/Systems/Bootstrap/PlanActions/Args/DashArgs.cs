namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// dash Action 的强类型参数
    /// </summary>
    public readonly struct DashArgs
    {
        /// <summary>
        /// 冲刺速度（单位/秒）
        /// </summary>
        public readonly float Speed;

        /// <summary>
        /// 冲刺持续时间（毫秒）
        /// </summary>
        public readonly float DurationMs;

        /// <summary>
        /// 冲刺方向模式
        /// 0=朝技能瞄准方向, 1=朝目标方向, 2=保持当前朝向
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 优先级（高优先级会打断低优先级运动）
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 是否应用到释放者（默认 caster）
        /// </summary>
        public readonly bool ApplyToCaster;

        public DashArgs(float speed, float durationMs, int directionMode = 0, int priority = 10, bool applyToCaster = true)
        {
            Speed = speed;
            DurationMs = durationMs;
            DirectionMode = directionMode;
            Priority = priority;
            ApplyToCaster = applyToCaster;
        }

        public static DashArgs Default => new DashArgs(0f, 0f, 0, 10, true);
    }
}
