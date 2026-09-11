#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 妫€娴嬭寖鍥村唴鏄惁鏈夌洰鏍?
    /// </summary>
    [BTConditionTypeId("HasTargetInRange")]
    public sealed class HasTargetInRangeCondition
    {
        public float Range { get; set; } = 10f;
    }

    /// <summary>
    /// 是否在攻击范围内
    /// </summary>
    [BTConditionTypeId("IsInAttackRange")]
    public sealed class IsInAttackRangeCondition
    {
        public float AttackRange { get; set; } = 1.5f;
    }

    /// <summary>
    /// 没有目标
    /// </summary>
    [BTConditionTypeId("NoTarget")]
    public sealed class NoTargetCondition { }

    /// <summary>
    /// 目标是否存活
    /// </summary>
    [BTConditionTypeId("IsTargetAlive")]
    public sealed class IsTargetAliveCondition { }

    /// <summary>
    /// 鏄惁鏈夎冻澶熻祫婧?
    /// </summary>
    [BTConditionTypeId("HasEnoughResource")]
    public sealed class HasEnoughResourceCondition
    {
        public float Cost { get; set; } = 20f;
    }

    /// <summary>
    /// 是否在冷却中
    /// </summary>
    [BTConditionTypeId("IsOnCooldown")]
    public sealed class IsOnCooldownCondition
    {
        public string SkillId { get; set; }
    }
}
