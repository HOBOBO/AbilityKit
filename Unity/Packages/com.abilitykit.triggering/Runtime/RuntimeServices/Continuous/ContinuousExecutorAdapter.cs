using System;
using AbilityKit.Continuous;

namespace AbilityKit.Triggering.Runtime.Continuous
{
    /// <summary>
    /// 持续执行器适配器
    /// 
    /// 将现有的 ContinuousExecutorBase 与新的 IContinuous 接口桥接。
    /// 使用此适配器可以让已有的 ContinuousExecutorBase 实现被 ContinuousManager 管理。
    /// 
    /// 使用方式：
    /// 1. 继承 ContinuousExecutorAdapter 而不是直接继承 ContinuousExecutorBase
    /// 2. 实现 CreateContext 方法
    /// 3. 在业务层通过 ContinuousManager 管理生命周期
    /// </summary>
    /// <typeparam name="TCtx">上下文类型</typeparam>
    public abstract class ContinuousExecutorAdapter<TCtx> : ContinuousExecutorBase<TCtx>, IContinuous
        where TCtx : class
    {
        #region IContinuous Members

        /// <inheritdoc />
        public IContinuousConfig Config { get; }

        /// <inheritdoc />
        public ContinuousState State { get; private set; } = ContinuousState.Inactive;

        /// <inheritdoc />
        public bool IsActive => State == ContinuousState.Active;

        /// <inheritdoc />
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;

        /// <inheritdoc />
        public bool IsPaused => State == ContinuousState.Paused;

        /// <inheritdoc />
        public float ElapsedSeconds => _elapsedMs / 1000f;

        /// <inheritdoc />
        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        private float _elapsedMs;
        private bool _started;

        /// <inheritdoc />
        public void Activate()
        {
            if (State != ContinuousState.Inactive)
                return;

            State = ContinuousState.Activating;
            _elapsedMs = 0;
            _started = false;
            State = ContinuousState.Active;
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (State != ContinuousState.Active)
                return;

            State = ContinuousState.Paused;
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (State != ContinuousState.Paused)
                return;

            State = ContinuousState.Active;
        }

        /// <inheritdoc />
        public void End(ContinuousEndReason reason)
        {
            if (IsTerminated)
                return;

            if (reason == ContinuousEndReason.Completed)
            {
                State = ContinuousState.Expired;
                var completedCtx = CreateContext();
                if (completedCtx != null)
                {
                    Terminate(EContinuousState.Completed, completedCtx);
                }

                OnExpiredCore();
                RaiseEnded(ContinuousEndReason.Completed);
                return;
            }

            State = ContinuousState.Aborted;
            var interruptedCtx = CreateContext();
            if (interruptedCtx != null)
            {
                Terminate(EContinuousState.Interrupted, interruptedCtx);
            }

            OnAbortedCore(reason.ToString());
            RaiseEnded(reason);
        }

        /// <inheritdoc />
        public void Abort(string reason)
        {
            if (IsTerminated)
                return;

            State = ContinuousState.Aborted;

            var ctx = CreateContext();
            if (ctx != null)
            {
                Terminate(EContinuousState.Interrupted, ctx);
            }

            OnAbortedCore(reason);
            RaiseEnded(ContinuousEndReason.Interrupted);
        }

        #endregion

        #region Override Points (For Subclasses)

        /// <summary>
        /// 创建执行上下文
        /// </summary>
        protected abstract TCtx CreateContext();

        /// <summary>
        /// 激活时被调用（子类可override）
        /// </summary>
        protected virtual void OnActivatedCore() { }

        /// <summary>
        /// 暂停时被调用（子类可override）
        /// </summary>
        protected virtual void OnPausedCore() { }

        /// <summary>
        /// 恢复时被调用（子类可override）
        /// </summary>
        protected virtual void OnResumedCore() { }

        /// <summary>
        /// 中止时被调用（子类可override）
        /// </summary>
        protected virtual void OnAbortedCore(string reason) { }

        /// <summary>
        /// 正常过期时被调用（子类可override）
        /// </summary>
        protected virtual void OnExpiredCore() { }

        #endregion

        #region Internal Update

        /// <summary>
        /// 内部更新方法，由外部系统（如 ContinuousExecutorAdapterManager）调用
        /// </summary>
        internal void InternalTick(float deltaTimeMs)
        {
            if (State != ContinuousState.Active)
                return;

            _elapsedMs += deltaTimeMs;

            // 首次执行时调用 OnStart
            if (!_started)
            {
                _started = true;
                var ctx = CreateContext();
                if (ctx != null)
                {
                    Start(ctx);
                    OnActivatedCore();
                }
            }

            var ctx2 = CreateContext();
            if (ctx2 != null)
            {
                Execute(deltaTimeMs, new ContinuousExecuteContextAdapter(_elapsedMs), ctx2);
            }

            // 检查时长（如果实现了 IDurationConfig）
            var durationConfig = Config as IDurationConfig;
            if (durationConfig?.DurationSeconds != null && _elapsedMs >= durationConfig.DurationSeconds.Value * 1000f)
            {
                Expire();
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// 触发正常结束
        /// </summary>
        protected void Expire()
        {
            End(ContinuousEndReason.Completed);
        }

        /// <summary>
        /// 触发结束事件
        /// </summary>
        protected void RaiseEnded(ContinuousEndReason reason)
        {
            OnEnded?.Invoke(this, reason);
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 创建持续执行器适配器
        /// </summary>
        protected ContinuousExecutorAdapter(IContinuousConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion

        #region Private Helper

        private class ContinuousExecuteContextAdapter : IContinuousTriggerInstance
        {
            public ContinuousExecuteContextAdapter(float elapsedMs)
            {
                ElapsedMs = elapsedMs;
            }

            public int InstanceId => 0;
            public int TriggerId => 0;
            public EContinuousState CurrentState => EContinuousState.Running;
            public int ExecutionCount => 0;
            public float ElapsedMs { get; }
            public float LastExecuteAtMs => ElapsedMs;
            public int MaxExecutions => -1;
            public bool CanBeInterrupted => true;
            public string InterruptReason => null;
            public bool IsCompleted => false;
            public bool IsTerminated => false;
        }

        #endregion
    }
}
