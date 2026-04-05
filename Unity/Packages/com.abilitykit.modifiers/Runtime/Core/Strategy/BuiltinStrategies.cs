using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 内置数值策略实现
    // ============================================================================

    /// <summary>
    /// 加法策略
    /// </summary>
    [StrategyImpl("numeric.add")]
    public sealed class NumericAddStrategy : NumericStrategyBase
    {
        public override StrategyId StrategyId => new StrategyId("numeric.add");

        public override StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            return StrategyApplyResult.Succeeded();
        }

        public override StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            return StrategyRevertResult.Succeeded();
        }
    }

    /// <summary>
    /// 乘法策略
    /// </summary>
    [StrategyImpl("numeric.mult")]
    public sealed class NumericMultStrategy : NumericStrategyBase
    {
        public override StrategyId StrategyId => new StrategyId("numeric.mult");

        public override StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            return StrategyApplyResult.Succeeded();
        }

        public override StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            return StrategyRevertResult.Succeeded();
        }
    }

    /// <summary>
    /// 覆盖策略
    /// </summary>
    [StrategyImpl("numeric.override")]
    public sealed class NumericOverrideStrategy : NumericStrategyBase
    {
        public override StrategyId StrategyId => new StrategyId("numeric.override");

        public override StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            return StrategyApplyResult.Succeeded();
        }

        public override StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            return StrategyRevertResult.Succeeded();
        }
    }

    /// <summary>
    /// 百分比加成策略
    /// </summary>
    [StrategyImpl("numeric.percent")]
    public sealed class NumericPercentStrategy : NumericStrategyBase
    {
        public override StrategyId StrategyId => new StrategyId("numeric.percent");

        public override StrategyApplyResult Apply(object target, in StrategyContext context)
        {
            return StrategyApplyResult.Succeeded();
        }

        public override StrategyRevertResult Revert(object target, in StrategyContext context)
        {
            return StrategyRevertResult.Succeeded();
        }
    }
}
