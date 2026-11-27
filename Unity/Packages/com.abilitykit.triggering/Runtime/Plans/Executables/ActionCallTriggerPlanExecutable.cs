using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 执行树的单动作叶子；每次使用当前上下文的注册表绑定动作。
    /// </summary>
    public sealed class ActionCallTriggerPlanExecutable : TriggerPlanExecutableBase
    {
        private readonly ActionCallPlan _action;
        private readonly int _originalActionIndex;

        /// <summary>
        /// 返回叶子持有的动作描述，不包含运行时绑定。
        /// </summary>
        public ActionCallPlan Action => _action;

        /// <summary>
        /// 表示动作使用不同于零号槽的作用域索引。
        /// </summary>

        /// <summary>
        /// 返回执行树诊断使用的节点名称。
        /// </summary>
        public override string Name => "ActionCall";

        /// <summary>
        /// 返回动作节点种类。
        /// </summary>
        public override ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Action;

        /// <summary>
        /// 创建动作叶子；未提供有效原始索引时，动作 Scope 使用零作为索引。
        /// </summary>
        public ActionCallTriggerPlanExecutable(ActionCallPlan action, int originalActionIndex = -1, ITriggerPlanCondition condition = null, float weight = 1f)
            : base(condition, weight)
        {
            _action = action;
            _originalActionIndex = originalActionIndex;
        }

        /// <summary>
        /// 绑定并调用动作；动作拒绝时返回失败，绑定和调用异常继续向上抛出。
        /// </summary>
        protected override TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            var binding = PlannedTriggerActionBindingResolver<object, TCtx>.Resolve(in _action, in ctx);
            var invoker = default(TriggerPlanActionInvoker<object, TCtx>);
            invoker.Execute(in args, in _action, in ctx, in binding, index: 0, scopeIndex: _originalActionIndex >= 0 ? _originalActionIndex : 0);
            if (ctx.Control != null && ctx.Control.IsActionRejected)
            {
                return TriggerPlanExecutionResult.Failed(
                    ctx.Control.ActionRejectReason ?? $"Action {_action.Id.Value} rejected");
            }

            return TriggerPlanExecutionResult.Success(1);
        }
    }
}
