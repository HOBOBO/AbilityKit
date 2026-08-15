using System;
using AbilityKit.Continuous;

namespace AbilityKit.Triggering.Runtime.Continuous
{
    /// <summary>
    /// 过程单元 (ProcessUnit)
    /// 
    /// 主动执行的持续体，继承 IContinuous 并附加主动执行能力。
    /// 管线 Tick 它，它有内部时钟和逐帧逻辑。
    /// 它响应 Pause/Resume/Abort 时，会把这些调用传递给内部的 Executor。
    /// 
    /// 与 IContinuousDecorator 的区别：
    /// - IContinuousDecorator: 专注于 OnApplied/OnTick/OnRemoved 的执行模式
    /// - ProcessUnit: 专注于生命周期管理 + 主动执行，是 IContinuous 的实现
    /// </summary>
    public abstract class ProcessUnit : IContinuous
    {
        #region IContinuous Members

        /// <inheritdoc />
        public IContinuousConfig Config { get; protected set; }

        /// <inheritdoc />
        public ContinuousState State { get; protected set; } = ContinuousState.Inactive;

        /// <inheritdoc />
        public bool IsActive => State == ContinuousState.Active;

        /// <inheritdoc />
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;

        /// <inheritdoc />
        public bool IsPaused => State == ContinuousState.Paused;

        /// <inheritdoc />
        public float ElapsedSeconds { get; protected set; }

        /// <inheritdoc />
        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        /// <inheritdoc />
        public virtual void Activate()
        {
            if (State != ContinuousState.Inactive)
                return;

            State = ContinuousState.Activating;
            OnActivated();
            State = ContinuousState.Active;
        }

        /// <inheritdoc />
        public virtual void Pause()
        {
            if (State != ContinuousState.Active)
                return;

            State = ContinuousState.Paused;
            OnPaused();
        }

        /// <inheritdoc />
        public virtual void Resume()
        {
            if (State != ContinuousState.Paused)
                return;

            State = ContinuousState.Active;
            OnResumed();
        }

        /// <inheritdoc />
        public virtual void End(ContinuousEndReason reason)
        {
            if (IsTerminated)
                return;

            if (reason == ContinuousEndReason.Completed)
            {
                State = ContinuousState.Expired;
                OnExpired();
                RaiseEnded(ContinuousEndReason.Completed);
                return;
            }

            State = ContinuousState.Aborted;
            OnAborted(reason.ToString());
            RaiseEnded(reason);
        }

        /// <inheritdoc />
        public virtual void Abort(string reason)
        {
            if (IsTerminated)
                return;

            State = ContinuousState.Aborted;
            OnAborted(reason);
            RaiseEnded(ContinuousEndReason.Interrupted);
        }

        #endregion

        #region Protected Virtual Methods (Override Points)

        /// <summary>
        /// 激活时被调用
        /// </summary>
        protected virtual void OnActivated() { }

        /// <summary>
        /// 暂停时被调用
        /// </summary>
        protected virtual void OnPaused() { }

        /// <summary>
        /// 恢复时被调用
        /// </summary>
        protected virtual void OnResumed() { }

        /// <summary>
        /// 中止时被调用
        /// </summary>
        protected virtual void OnAborted(string reason) { }

        /// <summary>
        /// 正常过期时被调用
        /// </summary>
        protected virtual void OnExpired() { }

        /// <summary>
        /// Tick 时被调用（仅在 Active 状态下）
        /// </summary>
        protected virtual void OnTick(float deltaTimeSeconds) { }

        #endregion

        #region Internal Update (Called by manager)

        /// <summary>
        /// 内部更新方法，由 ContinuousManager 或外部系统调用
        /// </summary>
        internal void InternalTick(float deltaTimeSeconds)
        {
            if (State != ContinuousState.Active)
                return;

            ElapsedSeconds += deltaTimeSeconds;
            OnTick(deltaTimeSeconds);

            // 检查时长（如果实现了 IDurationConfig）
            var durationConfig = Config as IDurationConfig;
            if (durationConfig?.DurationSeconds != null && ElapsedSeconds >= durationConfig.DurationSeconds.Value)
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
        /// 创建过程单元
        /// </summary>
        protected ProcessUnit(IContinuousConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion
    }
}
