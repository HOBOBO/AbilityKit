using System;
using AbilityKit.Triggering.Runtime.Executable;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 为旧策略保留的执行器，同步调用内部执行器并防止当前包装器实例重入。
    /// 不缓存后续请求，不提供跨实例队列、先进先出或优先级排序，不建议新计划使用。
    /// </summary>
    internal sealed class QueuedActionExecutor : ActionExecutorBase
    {
        private ActionExecutorBase _inner;
        private int _queuePriority;
        private bool _isQueued;

        /// <summary>
        /// 创建未初始化的包装器；在完成内部初始化之前，执行尝试返回失败。
        /// </summary>
        public QueuedActionExecutor()
        {
        }

        /// <summary>
        /// 创建包装器，使用指定的内部执行器进行同步调用。
        /// </summary>
        /// <param name="inner">需要包装的执行器。</param>
        /// <param name="queuePriority">仅为兼容而保存的值，不参与排序。</param>
        public QueuedActionExecutor(ActionExecutorBase inner, int queuePriority = 0)
        {
            Initialize(inner, queuePriority);
        }

        internal void Initialize(ActionExecutorBase inner, int queuePriority = 0)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _queuePriority = queuePriority;
            _isQueued = false;
        }

        internal ActionExecutorBase Inner => _inner;

        /// <summary>
        /// 首次进入时同步调用内部执行器；同一包装器重入时跳过，不保存本次请求。
        /// </summary>
        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            if (_inner == null)
            {
                return ExecutionResult.Failed("Queued executor is not initialized.");
            }

            if (_isQueued)
            {
                return ExecutionResult.Skipped("Action is already queued");
            }

            _isQueued = true;
            try
            {
                return _inner.TryExecute(ctx, out var result) ? result : ExecutionResult.Failed("Failed to execute from queue");
            }
            finally
            {
                _isQueued = false;
            }
        }

        /// <summary>
        /// 将取消原因转发给内部执行器，正常返回后清除防重入标记。
        /// </summary>
        public override void Cancel(string reason)
        {
            _inner?.Cancel(reason);
            _isQueued = false;
        }

        /// <summary>
        /// 返回为兼容而保存的优先级值；调度器不读取此值来排序。
        /// </summary>
        public int QueuePriority => _queuePriority;

        /// <summary>
        /// 仅表示当前包装器实例的防重入标记，不表示动作已进入等待队列。
        /// </summary>
        public bool IsQueued => _isQueued;

        internal override void ResetForPool()
        {
            _isQueued = false;
            _queuePriority = 0;
            _inner = null;
        }
    }
}
