using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 以当前执行上下文求值计划节点的布尔表达式。
    /// </summary>
    public sealed class PredicateExprTriggerPlanCondition : ITriggerPlanCondition
    {
        private readonly PredicateExprPlan _predicate;

        /// <summary>
        /// 使用指定表达式创建条件节点；空表达式视为条件成立。
        /// </summary>
        public PredicateExprTriggerPlanCondition(PredicateExprPlan predicate)
        {
            _predicate = predicate;
        }

        /// <summary>
        /// 使用本次事件参数求值，并保留表达式的确定性检查与异常传播。
        /// </summary>
        public bool Evaluate<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            return PlannedTriggerPredicateEvaluator<object, TCtx>.EvaluateExpression(in _predicate, in args, in ctx);
        }
    }
}
