#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 寰呮満鐘舵€?
    /// </summary>
    [StateTypeId("Idle")]
    public sealed class IdleState
    {
        public float PatrolRadius { get; set; } = 10f;
        public float DetectionRange { get; set; } = 15f;
    }

    /// <summary>
    /// 杩介€愮姸鎬?
    /// </summary>
    [StateTypeId("Chase")]
    public sealed class ChaseState
    {
        public float MoveSpeed { get; set; } = 5f;
        public float ChaseDistance { get; set; } = 2f;
    }

    /// <summary>
    /// 鏀诲嚮鐘舵€?
    /// </summary>
    [StateTypeId("Attack")]
    public sealed class AttackState
    {
        public float AttackRange { get; set; } = 1.5f;
        public float AttackDamage { get; set; } = 50f;
        public float AttackCooldown { get; set; } = 1.5f;
    }

    /// <summary>
    /// 姝讳骸鐘舵€?
    /// </summary>
    [StateTypeId("Dead")]
    public sealed class DeadState
    {
        public float FadeDuration { get; set; } = 2f;
    }
}
