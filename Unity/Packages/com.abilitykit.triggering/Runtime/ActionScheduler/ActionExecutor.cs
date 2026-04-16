using System;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Action 执行上下文
    /// 提供单次执行所需的全部环境和约束
    /// </summary>
    public readonly struct ActionExecutionContext
    {
        public readonly ActionInstance Instance;
        public readonly object GlobalContext;
        public readonly ITriggerDispatcherContext DispatcherContext;
        public readonly ExecutionControl Control;

        public ActionExecutionContext(
            ActionInstance instance,
            object globalContext,
            ITriggerDispatcherContext dispatcherContext,
            ExecutionControl control)
        {
            Instance = instance;
            GlobalContext = globalContext;
            DispatcherContext = dispatcherContext;
            Control = control;
        }
    }

    /// <summary>
    /// Action 执行器接口
    /// 负责单次 Action 执行的策略：检查队列、合法性、同步等
    /// </summary>
    public interface IActionExecutor
    {
        /// <summary>
        /// 尝试执行 Action
        /// 返回 false 表示不能执行（队列满、同步阻塞、条件不满足等）
        /// </summary>
        bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result);

        /// <summary>
        /// 取消/中断执行
        /// </summary>
        void Cancel(string reason);
    }

    /// <summary>
    /// 基础执行器（默认策略：立即执行）
    /// </summary>
    public abstract class ActionExecutorBase : IActionExecutor
    {
        public virtual bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result)
        {
            result = ExecuteCore(ctx);
            return result.IsSuccess;
        }

        /// <summary>
        /// 核心执行逻辑（子类实现）
        /// </summary>
        protected abstract ExecutionResult ExecuteCore(ActionExecutionContext ctx);

        public virtual void Cancel(string reason)
        {
            // 默认无操作
        }
    }

    /// <summary>
    /// 队列执行器（支持优先级和队列）
    /// </summary>
    public sealed class QueuedActionExecutor : ActionExecutorBase
    {
        private readonly ActionExecutorBase _inner;
        private readonly int _queuePriority;
        private bool _isQueued;

        public QueuedActionExecutor(ActionExecutorBase inner, int queuePriority = 0)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _queuePriority = queuePriority;
        }

        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            if (_isQueued)
            {
                return ExecutionResult.Failed("Action is already queued");
            }
            _isQueued = true;
            return _inner.TryExecute(ctx, out var result) ? result : ExecutionResult.Failed("Failed to execute from queue");
        }

        public override void Cancel(string reason)
        {
            _inner.Cancel(reason);
            _isQueued = false;
        }

        public int QueuePriority => _queuePriority;
    }

    /// <summary>
    /// 同步执行器（支持信号等待）
    /// </summary>
    public sealed class SynchronizedActionExecutor : ActionExecutorBase
    {
        private readonly ActionExecutorBase _inner;
        private readonly TriggerWaitHandle _waitHandle;

        public SynchronizedActionExecutor(ActionExecutorBase inner, TriggerWaitHandle waitHandle)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _waitHandle = waitHandle ?? throw new ArgumentNullException(nameof(waitHandle));
        }

        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            // 等待信号
            _waitHandle.WaitOne();

            if (!_waitHandle.IsSignaled)
            {
                return ExecutionResult.Skipped("Wait handle not signaled");
            }

            return _inner.TryExecute(ctx, out var result) ? result : ExecutionResult.Failed("Inner executor failed");
        }

        public override void Cancel(string reason)
        {
            _inner.Cancel(reason);
        }

        public void Signal()
        {
            _waitHandle.Set();
        }
    }

    /// <summary>
    /// 重试执行器（失败自动重试）
    /// </summary>
    public sealed class RetryActionExecutor : ActionExecutorBase
    {
        private readonly ActionExecutorBase _inner;
        private readonly int _maxRetries;
        private readonly float _retryDelayMs;

        public RetryActionExecutor(ActionExecutorBase inner, int maxRetries = 3, float retryDelayMs = 100f)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
        }

        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            int attempt = 0;
            while (attempt <= _maxRetries)
            {
                if (_inner.TryExecute(ctx, out var result))
                {
                    return result;
                }

                attempt++;
                if (attempt > _maxRetries)
                {
                    return ExecutionResult.Failed($"Failed after {_maxRetries} retries");
                }

                // 延迟后重试
                // TODO: 需要调度器支持延迟执行
            }

            return ExecutionResult.Failed("Retry logic error");
        }

        public override void Cancel(string reason)
        {
            _inner.Cancel(reason);
        }
    }
}
