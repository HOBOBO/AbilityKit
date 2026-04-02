using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 组合条件 - And
    /// </summary>
    public sealed class SkillAndCondition : ISkillCondition
    {
        public string Id => "and";
        public string DisplayName => "且";
        public string Description => string.Join(" 且 ", _conditions.Select(c => c.DisplayName));
        public bool SupportsContinuousCheck => _conditions.All(c => c.SupportsContinuousCheck);

        private readonly List<ISkillCondition> _conditions;

        public SkillAndCondition(params ISkillCondition[] conditions)
        {
            _conditions = conditions?.ToList() ?? new List<ISkillCondition>();
        }

        public SkillAndCondition(IEnumerable<ISkillCondition> conditions)
        {
            _conditions = conditions?.ToList() ?? new List<ISkillCondition>();
        }

        public SkillConditionResult Check(SkillPipelineContext context)
        {
            var result = SkillConditionResult.Pass;
            foreach (var condition in _conditions)
            {
                var r = condition?.Check(context) ?? SkillConditionResult.Pass;
                result = result.And(r);
                if (!result.Passed)
                    return result;
            }
            return result;
        }
    }

    /// <summary>
    /// 组合条件 - Or
    /// </summary>
    public sealed class SkillOrCondition : ISkillCondition
    {
        public string Id => "or";
        public string DisplayName => "或";
        public string Description => string.Join(" 或 ", _conditions.Select(c => c.DisplayName));
        public bool SupportsContinuousCheck => false; // Or 条件不支持持续检测

        private readonly List<ISkillCondition> _conditions;

        public SkillOrCondition(params ISkillCondition[] conditions)
        {
            _conditions = conditions?.ToList() ?? new List<ISkillCondition>();
        }

        public SkillOrCondition(IEnumerable<ISkillCondition> conditions)
        {
            _conditions = conditions?.ToList() ?? new List<ISkillCondition>();
        }

        public SkillConditionResult Check(SkillPipelineContext context)
        {
            var result = SkillConditionResult.Fail("所有条件都不满足", "all_failed");
            foreach (var condition in _conditions)
            {
                var r = condition?.Check(context) ?? SkillConditionResult.Pass;
                if (r.Passed)
                    return r;
            }
            return result;
        }
    }

    /// <summary>
    /// 组合条件 - Not
    /// </summary>
    public sealed class SkillNotCondition : ISkillCondition
    {
        public string Id => "not";
        public string DisplayName => "非";
        public string Description => $"非 {Inner?.DisplayName}";
        public bool SupportsContinuousCheck => Inner?.SupportsContinuousCheck ?? false;

        public ISkillCondition Inner { get; set; }

        public SkillNotCondition(ISkillCondition inner)
        {
            Inner = inner;
        }

        public SkillConditionResult Check(SkillPipelineContext context)
        {
            var inner = Inner?.Check(context) ?? SkillConditionResult.Pass;
            if (inner.Passed)
                return SkillConditionResult.Fail("条件不应满足", "not_expected");
            return SkillConditionResult.Pass;
        }
    }

    /// <summary>
    /// Lambda 条件（用于代码内联）
    /// </summary>
    [SkillCondition("lambda", "Lambda条件")]
    public sealed class LambdaSkillCondition : SkillConditionBase
    {
        private readonly Func<SkillPipelineContext, SkillConditionResult> _checker;
        private readonly Func<SkillPipelineContext, bool> _simpleChecker;
        private readonly string _failureReason;
        private readonly bool _useSimpleChecker;

        public LambdaSkillCondition(
            Func<SkillPipelineContext, bool> checker,
            string displayName = "Lambda",
            string failureReason = "条件不满足")
        {
            _simpleChecker = checker;
            _failureReason = failureReason;
            _useSimpleChecker = true;
        }

        public LambdaSkillCondition(
            Func<SkillPipelineContext, SkillConditionResult> checker,
            string displayName = "Lambda")
        {
            _checker = checker;
            _useSimpleChecker = false;
        }

        public override SkillConditionResult Check(SkillPipelineContext context)
        {
            if (_useSimpleChecker)
            {
                return _simpleChecker(context)
                    ? SkillConditionResult.Pass
                    : SkillConditionResult.Fail(_failureReason, "lambda_failed");
            }
            return _checker(context);
        }
    }
}