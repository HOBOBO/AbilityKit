using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 按调用方给定的上限逐条检查扁平计划及其顶层谓词。
    /// 不检查执行根、规则集合总量或运行期资源消耗。
    /// </summary>
    public sealed class FlatTriggerPlanLimitsValidator<TCtx> : ITriggerValidator<TCtx>
    {
        /// <summary>
        /// 返回组合诊断中使用的校验器名称。
        /// </summary>
        public string Name => "扁平计划限额校验";

        /// <summary>
        /// 在组合校验中的执行优先级。
        /// </summary>
        public int Priority => 3;

        /// <summary>
        /// 超额仍返回错误，但不会因此提前停止组合校验。
        /// </summary>
        public bool IsCritical => false;

        private readonly int _maxNestingDepth;
        private readonly int _maxNodeCount;
        private readonly int _maxComplexity;
        private readonly int _maxActionCount;

        /// <summary>
        /// 创建扁平计划限额校验器；上限必须显式给出，不从校验上下文读取。
        /// </summary>
        /// <param name="maxNestingDepth">最大嵌套深度</param>
        /// <param name="maxNodeCount">单个计划最大节点数</param>
        /// <param name="maxComplexity">最大复杂度</param>
        /// <param name="maxActionCount">最大 Action 数量</param>
        public FlatTriggerPlanLimitsValidator(
            int maxNestingDepth,
            int maxNodeCount,
            int maxComplexity,
            int maxActionCount)
        {
            _maxNestingDepth = maxNestingDepth;
            _maxNodeCount = maxNodeCount;
            _maxComplexity = maxComplexity;
            _maxActionCount = maxActionCount;
        }

        /// <summary>
        /// 返回每条扁平计划的静态限额诊断；不读取校验上下文中的配置。
        /// </summary>
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
}
