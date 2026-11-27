namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 持有本次执行的注册标记检查选项和宿主提供的时间值；不读取系统时钟。
    /// </summary>
    public readonly struct ExecPolicy
    {
        /// <summary>
        /// 启用计划绑定和条件求值路径上的确定性注册标记检查。
        /// </summary>
        /// <remarks>
        /// 不检查委托内部行为，也不禁止使用宿主注入的随机源。
        /// 扁平动作复用首次成功绑定的缓存；改变策略时应重新装配规则实例。
        /// </remarks>
        public readonly bool RequireDeterministic;

        /// <summary>
        /// 宿主提供的时间增量，单位为毫秒。
        /// </summary>
        public readonly float DeltaTimeMs;

        /// <summary>
        /// 宿主提供的累计时间，单位为毫秒。
        /// </summary>
        public readonly float TotalTimeMs;

        /// <summary>
        /// 保存宿主给定的时间值和注册标记检查选项，不转换或验证时间。
        /// </summary>
        public ExecPolicy(bool requireDeterministic, float deltaTimeMs = 0, float totalTimeMs = 0)
        {
            RequireDeterministic = requireDeterministic;
            DeltaTimeMs = deltaTimeMs;
            TotalTimeMs = totalTimeMs;
        }

        /// <summary>
        /// 返回不启用确定性注册标记检查、时间值为零的策略。
        /// </summary>
        public static ExecPolicy Default => default;

        /// <summary>
        /// 返回启用确定性注册标记检查、时间值为零的策略。
        /// </summary>
        public static ExecPolicy DeterministicOnly => new ExecPolicy(true);
    }
}
