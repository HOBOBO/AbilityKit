#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 鏄惁鏈夎冻澶熺殑榄旀硶鍊?
    /// </summary>
    [SkillConditionTypeId("HasEnoughMana")]
    public sealed class HasEnoughManaCondition
    {
        public float RequiredMana { get; set; } = 30f;
    }

    /// <summary>
    /// 目标是否在范围内
    /// </summary>
    [SkillConditionTypeId("TargetInRange")]
    public sealed class TargetInRangeCondition
    {
        public float MinRange { get; set; } = 5f;
        public float MaxRange { get; set; } = 30f;
    }

    /// <summary>
    /// 鏄惁涓嶅湪鍐峰嵈涓?
    /// </summary>
    [SkillConditionTypeId("NotOnCooldown")]
    public sealed class NotOnCooldownCondition
    {
        public string SkillId { get; set; }
    }

    /// <summary>
    /// 目标是否有效
    /// </summary>
    [SkillConditionTypeId("TargetValid")]
    public sealed class TargetValidCondition { }

    /// <summary>
    /// 鏄惁琚矇榛?
    /// </summary>
    [SkillConditionTypeId("NotSilenced")]
    public sealed class NotSilencedCondition { }

    /// <summary>
    /// 鏄惁鏈夎冻澶熺殑鐢熷懡鍊?
    /// </summary>
    [SkillConditionTypeId("HasEnoughHealth")]
    public sealed class HasEnoughHealthCondition
    {
        public float RequiredHealthPercent { get; set; } = 0.3f;
    }
}
