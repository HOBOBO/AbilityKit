#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public enum ShooterRvoExecutionMode
    {
        Disabled = 0,
        Managed = 1,
        AcceleratedPreferred = 2
    }

    public sealed class ShooterRvoOptions
    {
        public const float DefaultAgentRadius = 0.56f;
        public const float DefaultNeighborDistance = 2.5f;
        public const int DefaultMaxNeighbors = 12;
        public const float DefaultTimeHorizon = 1.25f;
        public const float DefaultMaxAcceleration = 12f;
        public const int DefaultAcceleratedValidationInterval = 30;

        public static ShooterRvoOptions Disabled { get; } = new ShooterRvoOptions(ShooterRvoExecutionMode.Disabled);

        public static ShooterRvoOptions Default { get; } = new ShooterRvoOptions(ShooterRvoExecutionMode.AcceleratedPreferred);

        public ShooterRvoOptions(
            ShooterRvoExecutionMode mode,
            float agentRadius = DefaultAgentRadius,
            float neighborDistance = DefaultNeighborDistance,
            int maxNeighbors = DefaultMaxNeighbors,
            float timeHorizon = DefaultTimeHorizon,
            float maxAcceleration = DefaultMaxAcceleration)
        {
            Mode = mode;
            AgentRadius = IsFinitePositive(agentRadius) ? agentRadius : DefaultAgentRadius;
            NeighborDistance = IsFinitePositive(neighborDistance)
                ? Math.Max(neighborDistance, AgentRadius * 2f)
                : DefaultNeighborDistance;
            MaxNeighbors = Math.Max(1, maxNeighbors);
            TimeHorizon = IsFinitePositive(timeHorizon) ? timeHorizon : DefaultTimeHorizon;
            MaxAcceleration = IsFinitePositive(maxAcceleration) ? maxAcceleration : DefaultMaxAcceleration;
        }

        public ShooterRvoExecutionMode Mode { get; }

        public bool Enabled => Mode != ShooterRvoExecutionMode.Disabled;

        public bool PreferAcceleration => Mode == ShooterRvoExecutionMode.AcceleratedPreferred;

        public float AgentRadius { get; }

        public float NeighborDistance { get; }

        public int MaxNeighbors { get; }

        public float TimeHorizon { get; }

        public float MaxAcceleration { get; }

        /// <summary>
        /// 加速邻居输出全量校验的采样间隔（按加速收集次数计；1 = 每次都校验）。
        /// 加速实现与托管路径按状态哈希逐字节对齐，生产用默认 30（约 1 秒 @30Hz）
        /// 作为漂移保险丝；确定性对照测试可设为 1。
        /// </summary>
        public int AcceleratedValidationInterval { get; set; } = DefaultAcceleratedValidationInterval;

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
