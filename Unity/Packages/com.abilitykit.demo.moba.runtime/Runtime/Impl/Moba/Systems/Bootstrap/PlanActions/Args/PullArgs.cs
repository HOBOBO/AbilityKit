namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// pull Action 的强类型参数
    /// </summary>
    public readonly struct PullArgs
    {
        /// <summary>
        /// 拉力速度（单位/秒）
        /// </summary>
        public readonly float Speed;

        /// <summary>
        /// 拉取持续时间（毫秒）
        /// </summary>
        public readonly float DurationMs;

        /// <summary>
        /// 拉取方向模式
        /// 0=从目标拉到技能释放者, 1=从目标拉到指定距离, 2=垂直向上拉
        /// </summary>
        public readonly int DirectionMode;

        /// <summary>
        /// 目标距离（DirectionMode=1时使用）
        /// </summary>
        public readonly float TargetDistance;

        /// <summary>
        /// 优先级
        /// </summary>
        public readonly int Priority;

        public PullArgs(float speed, float durationMs, int directionMode = 0, float targetDistance = 0f, int priority = 12)
        {
            Speed = speed;
            DurationMs = durationMs;
            DirectionMode = directionMode;
            TargetDistance = targetDistance;
            Priority = priority;
        }

        public static PullArgs Default => new PullArgs(0f, 0f, 0, 0f, 12);
    }
}
