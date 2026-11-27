using System;
using AbilityKit.Core.Pooling;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 内置扁平动作调度器专用的对象池，持有独立作用域。
    /// 宿主须先清空使用该池的调度器，再释放池；释放池不会取消动作或调用动作回调。
    /// </summary>
    public sealed class ActionSchedulerPools : IDisposable
    {
        private const string DefaultScopeName = "triggering-action-scheduler";

        private static readonly PoolKey ActionInstanceKey = new PoolKey("triggering.action-instance");
        private static readonly PoolKey DefaultExecutorKey = new PoolKey("triggering.action-executor.default");
        private static readonly PoolKey QueuedExecutorKey = new PoolKey("triggering.action-executor.queued");
        private static readonly PoolKey RetryExecutorKey = new PoolKey("triggering.action-executor.retry");

        private bool _disposed;

        /// <summary>
        /// 按默认配置创建独立池作用域，名称仅用于诊断，不用于共享池。
        /// </summary>
        public ActionSchedulerPools(string scopeName = DefaultScopeName)
            : this(scopeName, ActionSchedulerPoolProfile.Default)
        {
        }

        /// <summary>
        /// 使用指定的实现配置创建独立池作用域，供扩展内部和测试使用。
        /// </summary>
        internal ActionSchedulerPools(string scopeName, ActionSchedulerPoolProfile profile)
        {
            Scope = new PoolScope(scopeName);
            Profile = profile.IsSpecified ? profile : ActionSchedulerPoolProfile.Default;
        }

        internal PoolScope Scope { get; }

        /// <summary>
        /// 创建池时选用的默认容量、上限和预热配置。
        /// </summary>
        internal ActionSchedulerPoolProfile Profile { get; }

        /// <summary>
        /// 按配置预热动作实例和执行器，不登记或推进动作。
        /// </summary>
        public void Prewarm()
        {
            ThrowIfDisposed();
            GetActionInstancePool().Prewarm(Profile.ActionInstanceConfig.PrewarmCount);
            GetDefaultExecutorPool().Prewarm(Profile.DefaultExecutorConfig.PrewarmCount);
            GetQueuedExecutorPool().Prewarm(Profile.QueuedExecutorConfig.PrewarmCount);
            GetRetryExecutorPool().Prewarm(Profile.RetryExecutorConfig.PrewarmCount);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取本作用域的诊断快照，不枚举调度中的动作实例。
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            ThrowIfDisposed();
            return Scope.GetDebugSnapshots();
        }
#endif

        internal ActionInstance RentActionInstance(
            int instanceId,
            int triggerId,
            in ActionCallPlan plan,
            IActionExecutor executor,
            object globalContext,
            float createdAtMs,
            bool ownsExecutor)
        {
            ThrowIfDisposed();
            var instance = GetActionInstancePool().Get();
            instance.Initialize(instanceId, triggerId, in plan, executor, globalContext, createdAtMs, ownsExecutor);
            return instance;
        }

        internal DefaultActionExecutor RentDefaultActionExecutor(Action<object, ITriggerDispatcherContext> action)
        {
            ThrowIfDisposed();
            var executor = GetDefaultExecutorPool().Get();
            executor.Initialize(action);
            return executor;
        }

        internal QueuedActionExecutor RentQueuedActionExecutor(ActionExecutorBase inner, int queuePriority = 0)
        {
            ThrowIfDisposed();
            var executor = GetQueuedExecutorPool().Get();
            executor.Initialize(inner, queuePriority);
            return executor;
        }

        internal RetryActionExecutor RentRetryActionExecutor(ActionExecutorBase inner, int maxRetries = 3, float retryDelayMs = 0f)
        {
            ThrowIfDisposed();
            var executor = GetRetryExecutorPool().Get();
            executor.Initialize(inner, maxRetries, retryDelayMs);
            return executor;
        }

        internal void ReleaseActionInstance(ActionInstance instance)
        {
            if (instance == null) return;
            ThrowIfDisposed();

            var executor = instance.Executor;
            var ownsExecutor = instance.OwnsExecutor;
            instance.ResetForPool();
            GetActionInstancePool().Release(instance);

            if (ownsExecutor)
            {
                ReleaseActionExecutor(executor);
            }
        }

        internal void ReleaseActionExecutor(IActionExecutor executor)
        {
            if (executor == null) return;
            ThrowIfDisposed();

            switch (executor)
            {
                case RetryActionExecutor retry:
                {
                    var inner = retry.Inner;
                    retry.ResetForPool();
                    GetRetryExecutorPool().Release(retry);
                    ReleaseActionExecutor(inner);
                    break;
                }
                case QueuedActionExecutor queued:
                {
                    var inner = queued.Inner;
                    queued.ResetForPool();
                    GetQueuedExecutorPool().Release(queued);
                    ReleaseActionExecutor(inner);
                    break;
                }
                case DefaultActionExecutor defaultExecutor:
                    defaultExecutor.ResetForPool();
                    GetDefaultExecutorPool().Release(defaultExecutor);
                    break;
            }
        }

        /// <summary>
        /// 释放此池独占的作用域；调用前须清空所有借用此池的调度器。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Scope.Dispose();
        }

        private ObjectPool<ActionInstance> GetActionInstancePool()
        {
            return Scope.GetPool(ActionInstanceKey, () => new ActionInstance(), Profile.ActionInstanceConfig);
        }

        private ObjectPool<DefaultActionExecutor> GetDefaultExecutorPool()
        {
            return Scope.GetPool(DefaultExecutorKey, () => new DefaultActionExecutor(), Profile.DefaultExecutorConfig);
        }

        private ObjectPool<QueuedActionExecutor> GetQueuedExecutorPool()
        {
            return Scope.GetPool(QueuedExecutorKey, () => new QueuedActionExecutor(), Profile.QueuedExecutorConfig);
        }

        private ObjectPool<RetryActionExecutor> GetRetryExecutorPool()
        {
            return Scope.GetPool(RetryExecutorKey, () => new RetryActionExecutor(), Profile.RetryExecutorConfig);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ActionSchedulerPools));
        }
    }

    /// <summary>
    /// 内置扁平动作调度器各池的配置。
    /// </summary>
    internal readonly struct ActionSchedulerPoolProfile
    {
        private readonly bool _specified;

        /// <summary>
        /// 调度动作实例的池配置。
        /// </summary>
        public readonly PoolItemConfig ActionInstanceConfig;

        /// <summary>
        /// 默认动作执行器的池配置。
        /// </summary>
        public readonly PoolItemConfig DefaultExecutorConfig;

        /// <summary>
        /// 兼容防重入包装器的池配置。
        /// </summary>
        public readonly PoolItemConfig QueuedExecutorConfig;

        /// <summary>
        /// 重试包装器的池配置。
        /// </summary>
        public readonly PoolItemConfig RetryExecutorConfig;

        /// <summary>
        /// 分别为动作实例和默认、队列、重试执行器指定池配置。
        /// </summary>
        public ActionSchedulerPoolProfile(
            PoolItemConfig actionInstanceConfig,
            PoolItemConfig defaultExecutorConfig,
            PoolItemConfig queuedExecutorConfig,
            PoolItemConfig retryExecutorConfig)
        {
            _specified = true;
            ActionInstanceConfig = actionInstanceConfig;
            DefaultExecutorConfig = defaultExecutorConfig;
            QueuedExecutorConfig = queuedExecutorConfig;
            RetryExecutorConfig = retryExecutorConfig;
        }

        internal bool IsSpecified => _specified;

        /// <summary>
        /// 默认池容量和预热配置。
        /// </summary>
        public static ActionSchedulerPoolProfile Default => new ActionSchedulerPoolProfile(
            PoolItemConfig.Default(defaultCapacity: 32, maxSize: 4096, prewarmCount: 32),
            PoolItemConfig.Default(defaultCapacity: 32, maxSize: 4096, prewarmCount: 32),
            PoolItemConfig.Default(defaultCapacity: 8, maxSize: 1024, prewarmCount: 8),
            PoolItemConfig.Default(defaultCapacity: 8, maxSize: 1024, prewarmCount: 8));

        /// <summary>
        /// 适合低并发宿主的小容量及预热配置。
        /// </summary>
        public static ActionSchedulerPoolProfile Small => new ActionSchedulerPoolProfile(
            PoolItemConfig.Default(defaultCapacity: 4, maxSize: 256, prewarmCount: 4),
            PoolItemConfig.Default(defaultCapacity: 4, maxSize: 256, prewarmCount: 4),
            PoolItemConfig.Default(defaultCapacity: 2, maxSize: 128, prewarmCount: 2),
            PoolItemConfig.Default(defaultCapacity: 2, maxSize: 128, prewarmCount: 2));
    }
}
