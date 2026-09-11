#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 检测到敌人条件
    /// </summary>
    [TransitionConditionTypeId("EnemyDetected")]
    public sealed class EnemyDetectedCondition { }

    /// <summary>
    /// 在攻击范围内条件
    /// </summary>
    [TransitionConditionTypeId("InAttackRange")]
    public sealed class InAttackRangeCondition { }

    /// <summary>
    /// 目标丢失条件
    /// </summary>
    [TransitionConditionTypeId("TargetLost")]
    public sealed class TargetLostCondition { }

    /// <summary>
    /// 目标存活条件
    /// </summary>
    [TransitionConditionTypeId("TargetAlive")]
    public sealed class TargetAliveCondition { }

    /// <summary>
    /// 目标死亡条件
    /// </summary>
    [TransitionConditionTypeId("TargetDead")]
    public sealed class TargetDeadCondition { }
}
