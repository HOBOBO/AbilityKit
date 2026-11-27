using System;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Executable;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 默认动作执行器，使用实例的绑定参数调用调度委托，并将委托异常转换为失败结果。
    /// </summary>
    internal sealed class DefaultActionExecutor : ActionExecutorBase
    {
        private Action<object, ITriggerDispatcherContext> _action;

        /// <summary>
        /// 构造供对象池使用的未绑定执行器；租用后必须初始化。
        /// </summary>
        internal DefaultActionExecutor()
        {
        }

        /// <summary>
        /// 构造无池路径使用的执行器，并立即绑定非空委托。
        /// </summary>
        public DefaultActionExecutor(Action<object, ITriggerDispatcherContext> action)
        {
            Initialize(action);
        }

        /// <summary>
        /// 初始化刚租用的执行器，绑定本次使用的委托。
        /// </summary>
        internal void Initialize(Action<object, ITriggerDispatcherContext> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// 执行已绑定的委托；未初始化或委托抛出异常时返回失败。
        /// </summary>
        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            if (_action == null)
            {
                return ExecutionResult.Failed("Default action executor is not initialized.");
            }

            try
            {
                _action(ctx.Instance.BoundArgs, ctx.DispatcherContext);
                return ExecutionResult.Success();
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"Action execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// 归还对象池时断开委托引用，不调用它。
        /// </summary>
        internal override void ResetForPool()
        {
            _action = null;
        }
    }
}
