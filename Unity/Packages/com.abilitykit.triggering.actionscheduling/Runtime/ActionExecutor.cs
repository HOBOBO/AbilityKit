using System;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Action 执行上下文
    /// 提供单次执行所需的全部环境和约束
    /// </summary>
    internal readonly struct ActionExecutionContext
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
    /// 按执行器自身的策略尝试执行或取消单次 Action。
    /// </summary>
    internal interface IActionExecutor
    {
        /// <summary>
        /// 尝试执行 Action；返回 false 表示本次未成功执行，具体结果见 result。
        /// </summary>
        bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result);

        /// <summary>
        /// 取消/中断执行。
        /// </summary>
        void Cancel(string reason);
    }

    /// <summary>
    /// 基础执行器（默认策略：立即执行）。
    /// </summary>
    internal abstract class ActionExecutorBase : IActionExecutor
    {
        public virtual bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result)
        {
            result = ExecuteCore(ctx);
            return result.IsSuccess;
        }

        /// <summary>
        /// 核心执行逻辑（子类实现）。
        /// </summary>
        protected abstract ExecutionResult ExecuteCore(ActionExecutionContext ctx);

        public virtual void Cancel(string reason)
        {
            // 默认无操作
        }

        internal virtual void ResetForPool()
        {
        }
    }

    /// <summary>
    /// 重试执行器（失败自动重试）。
    /// </summary>
    internal sealed class RetryActionExecutor : ActionExecutorBase
    {
        private ActionExecutorBase _inner;
        private int _maxRetries;
        private float _retryDelayMs;
        private int _attemptCount;
        private float _nextRetryAtMs;
        private bool _hasPendingRetry;
        private bool _isCancelled;
        private ExecutionResult _lastResult;

        public RetryActionExecutor()
        {
            _lastResult = ExecutionResult.Failed("Retry not executed");
        }

        public RetryActionExecutor(ActionExecutorBase inner, int maxRetries = 3, float retryDelayMs = 0f)
        {
            Initialize(inner, maxRetries, retryDelayMs);
        }

        internal void Initialize(ActionExecutorBase inner, int maxRetries = 3, float retryDelayMs = 0f)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (retryDelayMs < 0f) throw new ArgumentOutOfRangeException(nameof(retryDelayMs));

            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
            _attemptCount = 0;
            _nextRetryAtMs = 0f;
            _hasPendingRetry = false;
            _isCancelled = false;
            _lastResult = ExecutionResult.Failed("Retry not executed");
        }

        internal ActionExecutorBase Inner => _inner;

        public override bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result)
        {
            if (_inner == null)
            {
                result = ExecutionResult.Failed("Retry executor is not initialized.");
                return false;
            }

            if (_isCancelled)
            {
                result = ExecutionResult.Failed("Retry action was cancelled");
                return false;
            }

            if (_retryDelayMs <= 0f)
            {
                result = ExecuteCore(ctx);
                return result.IsSuccess;
            }

            if (_hasPendingRetry && ctx.Instance.ElapsedMs < _nextRetryAtMs)
            {
                result = ExecutionResult.None;
                return false;
            }

            _hasPendingRetry = false;
            _inner.TryExecute(ctx, out _lastResult);

            if (_lastResult.IsSuccess)
            {
                ResetRetryState();
                result = _lastResult;
                return true;
            }

            if (_attemptCount >= _maxRetries)
            {
                result = ExecutionResult.Failed($"Failed after {_maxRetries} retries. Last error: {_lastResult.FailureReason}");
                return false;
            }

            _attemptCount++;
            _nextRetryAtMs = ctx.Instance.ElapsedMs + _retryDelayMs;
            _hasPendingRetry = true;
            result = ExecutionResult.None;
            return false;
        }

        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            if (_inner == null)
            {
                return ExecutionResult.Failed("Retry executor is not initialized.");
            }

            ExecutionResult lastResult = ExecutionResult.Failed("Retry not executed");
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                _inner.TryExecute(ctx, out lastResult);
                if (lastResult.IsSuccess)
                {
                    return lastResult;
                }
            }

            return ExecutionResult.Failed($"Failed after {_maxRetries} retries. Last error: {lastResult.FailureReason}");
        }

        public override void Cancel(string reason)
        {
            _isCancelled = true;
            _hasPendingRetry = false;
            _inner?.Cancel(reason);
        }

        internal override void ResetForPool()
        {
            _attemptCount = 0;
            _nextRetryAtMs = 0f;
            _hasPendingRetry = false;
            _isCancelled = false;
            _lastResult = ExecutionResult.Failed("Retry not executed");
            _inner = null;
            _maxRetries = 0;
            _retryDelayMs = 0f;
        }

        private void ResetRetryState()
        {
            _attemptCount = 0;
            _nextRetryAtMs = 0f;
            _hasPendingRetry = false;
            _lastResult = ExecutionResult.Failed("Retry not executed");
        }
    }
}
