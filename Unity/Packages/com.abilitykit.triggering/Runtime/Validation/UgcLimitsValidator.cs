using System;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// UGC 限制校验器
    /// 检测嵌套深度、节点数量、表达式复杂度等是否符合限制
    /// </summary>
    public sealed class UgcLimitsValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "UGC 限制校验";
        public int Priority => 3;
        public bool IsCritical => false;

        private readonly int _maxNestingDepth;
        private readonly int _maxNodeCount;
        private readonly int _maxComplexity;
        private readonly int _maxActionCount;

        /// <summary>
        /// 创建 UGC 限制校验器
        /// </summary>
        /// <param name="maxNestingDepth">最大嵌套深度</param>
        /// <param name="maxNodeCount">单个计划最大节点数</param>
        /// <param name="maxComplexity">最大复杂度</param>
        /// <param name="maxActionCount">最大 Action 数量</param>
        public UgcLimitsValidator(
            int maxNestingDepth = 10,
            int maxNodeCount = 100,
            int maxComplexity = 50,
            int maxActionCount = 20)
        {
            _maxNestingDepth = maxNestingDepth;
            _maxNodeCount = maxNodeCount;
            _maxComplexity = maxComplexity;
            _maxActionCount = maxActionCount;
        }

        /// <summary>
        /// 创建严格的 UGC 校验器
        /// </summary>
        public static UgcLimitsValidator<TCtx> Strict => new UgcLimitsValidator<TCtx>(
            maxNestingDepth: 5,
            maxNodeCount: 50,
            maxComplexity: 30,
            maxActionCount: 10);

        /// <summary>
        /// 创建宽松的 UGC 校验器
        /// </summary>
        public static UgcLimitsValidator<TCtx> Relaxed => new UgcLimitsValidator<TCtx>(
            maxNestingDepth: 20,
            maxNodeCount: 200,
            maxComplexity: 100,
            maxActionCount: 50);

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            foreach (var entry in database.Plans)
            {
                ValidateEntry(entry, context, ref result);
            }

            return result;
        }

        private void ValidateEntry(TriggerPlanEntry<TCtx> entry, in ValidationContext<TCtx> context, ref ValidationResult result)
        {
            var path = entry.GetPath();
            var plan = entry.Plan;

            // 检查 Action 数量
            if (plan.Actions != null && plan.Actions.Length > _maxActionCount)
            {
                result.AddError(
                    ValidationErrorCodes.EXCEEDS_ACTION_COUNT,
                    $"Action 数量 {plan.Actions.Length} 超过限制 {_maxActionCount}",
                    $"{path}.actions");
            }

            // 检查节点数
            var nodeCount = TriggerPlanAnalyzer.CountTotalNodes<TCtx>(plan);
            if (nodeCount > _maxNodeCount)
            {
                result.AddError(
                    ValidationErrorCodes.EXCEEDS_NODE_COUNT,
                    $"节点数 {nodeCount} 超过限制 {_maxNodeCount}",
                    path);
            }
            else if (nodeCount > _maxNodeCount * 0.8)
            {
                result.AddWarning(
                    ValidationErrorCodes.EXCEEDS_NODE_COUNT,
                    $"节点数 {nodeCount} 接近限制 {_maxNodeCount}",
                    path);
            }

            // 检查嵌套深度
            if (plan.HasPredicate && plan.PredicateKind == EPredicateKind.Expr)
            {
                var depth = TriggerPlanAnalyzer.CalculatePredicateDepth(plan.PredicateExpr);
                if (depth > _maxNestingDepth)
                {
                    result.AddError(
                        ValidationErrorCodes.EXCEEDS_NESTING_DEPTH,
                        $"嵌套深度 {depth} 超过限制 {_maxNestingDepth}",
                        $"{path}.predicate");
                }
                else if (depth > _maxNestingDepth * 0.8)
                {
                    result.AddWarning(
                        ValidationErrorCodes.EXCEEDS_NESTING_DEPTH,
                        $"嵌套深度 {depth} 接近限制 {_maxNestingDepth}",
                        $"{path}.predicate");
                }
            }

            // 检查复杂度
            var complexity = TriggerPlanAnalyzer.CalculatePlanComplexity<TCtx>(plan);
            if (complexity > _maxComplexity)
            {
                result.AddError(
                    ValidationErrorCodes.EXCEEDS_COMPLEXITY,
                    $"表达式复杂度 {complexity} 超过限制 {_maxComplexity}",
                    path);
            }
            else if (complexity > _maxComplexity * 0.7)
            {
                result.AddWarning(
                    ValidationErrorCodes.HIGH_COMPLEXITY,
                    $"表达式复杂度 {complexity} 较高，接近限制 {_maxComplexity}",
                    path);
            }

            // 检查空计划
            if (TriggerPlanAnalyzer.IsEmptyPlan<TCtx>(plan))
            {
                result.AddWarning(
                    ValidationErrorCodes.EMPTY_ACTION_LIST,
                    $"触发器计划为空（无谓词、无 Action）",
                    path);
            }
        }
    }

    /// <summary>
    /// 语义校验器
    /// 检测确定性不匹配、死代码等语义问题
    /// </summary>
    public sealed class SemanticValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "语义校验";
        public int Priority => 4;
        public bool IsCritical => false;

        private readonly bool _requireDeterministic;

        public SemanticValidator(bool requireDeterministic = true)
        {
            _requireDeterministic = requireDeterministic;
        }

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            foreach (var entry in database.Plans)
            {
                ValidateEntry(entry, ref result);
            }

            return result;
        }

        private void ValidateEntry(TriggerPlanEntry<TCtx> entry, ref ValidationResult result)
        {
            var path = entry.GetPath();
            var plan = entry.Plan;

            // 检查空 Action 列表
            if (plan.Actions == null || plan.Actions.Length == 0)
            {
                result.AddWarning(
                    ValidationErrorCodes.EMPTY_ACTION_LIST,
                    $"触发器没有配置任何 Action",
                    path);
            }

            // 检查无条件的触发器
            if (!plan.HasPredicate)
            {
                result.AddWarning(
                    ValidationErrorCodes.VOID_PREDICATE_RESULT,
                    $"触发器没有条件判断，所有事件都会触发",
                    path);
            }
        }
    }

    /// <summary>
    /// 死代码检测校验器
    /// 检测永不执行的代码分支
    /// </summary>
    public sealed class DeadCodeValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "死代码检测";
        public int Priority => 5;
        public bool IsCritical => false;

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            foreach (var entry in database.Plans)
            {
                ValidateEntry(entry, ref result);
            }

            return result;
        }

        private void ValidateEntry(TriggerPlanEntry<TCtx> entry, ref ValidationResult result)
        {
            var path = entry.GetPath();
            var plan = entry.Plan;

            if (plan.HasPredicate && plan.PredicateKind == EPredicateKind.Expr)
            {
                if (plan.PredicateExpr.Nodes != null)
                {
                    var hasDeadBranch = CheckForDeadCode(plan.PredicateExpr);
                    if (hasDeadBranch)
                    {
                        result.AddWarning(
                            ValidationErrorCodes.DEAD_CODE_BRANCH,
                            $"检测到可能永不执行的代码分支",
                            $"{path}.predicate");
                    }
                }
            }
        }

        private bool CheckForDeadCode(PredicateExprPlan expr)
        {
            if (expr.Nodes == null || expr.Nodes.Length == 0)
                return false;

            for (int i = 0; i < expr.Nodes.Length - 1; i++)
            {
                if (expr.Nodes[i].Kind == EBoolExprNodeKind.Const && !expr.Nodes[i].ConstValue)
                {
                    if (i + 1 < expr.Nodes.Length && expr.Nodes[i + 1].Kind == EBoolExprNodeKind.And)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
