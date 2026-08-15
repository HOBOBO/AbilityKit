using System;
using AbilityKit.Continuous;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 持续运行时实现共享的状态机基类。
    /// 计时状态（已流逝/间隔剩余）以 Q32.32 raw long 累加（整数运算无漂移），
    /// float 属性是 IContinuous 接口/表现边界的单次换算视图。
    /// </summary>
    public abstract class MobaContinuousRuntimeBase : IContinuous
    {
        private long _elapsedRaw;
        private long _intervalRemainingRaw;

        public abstract IContinuousConfig Config { get; }

        public ContinuousState State { get; private set; } = ContinuousState.Inactive;
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;

        public float ElapsedSeconds => Fixed64.FromRaw(_elapsedRaw).ToSingle();

        /// <summary>已流逝秒数的 Q32.32 raw（内部累加与回滚恢复用）。</summary>
        internal long ElapsedRaw => _elapsedRaw;

        /// <summary>距下次间隔触发的剩余秒数（Q32.32 raw；TickProcessor 整数运算用）。</summary>
        internal long IntervalRemainingRaw
        {
            get => _intervalRemainingRaw;
            set => _intervalRemainingRaw = value;
        }

        /// <summary>
        /// 间隔剩余的 float 视图（IMobaContinuousIntervalState 落点）。
        /// setter 单次换算——只应在初始化/配置回填时写，逐帧推进走 raw 路径。
        /// </summary>
        public float IntervalRemainingSeconds
        {
            get => Fixed64.FromRaw(_intervalRemainingRaw).ToSingle();
            set => _intervalRemainingRaw = DeterministicMathBridge.ToFixed(value).RawValue;
        }

        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        public void Activate()
        {
            if (State == ContinuousState.Active) return;
            if (IsTerminated) return;

            State = ContinuousState.Activating;
            if (!OnActivating())
            {
                CompleteEnd(ContinuousEndReason.Interrupted);
                return;
            }

            State = ContinuousState.Active;
            OnActivated();
        }

        public void Pause()
        {
            if (State != ContinuousState.Active) return;
            State = ContinuousState.Paused;
            OnPaused();
        }

        public void Resume()
        {
            if (State != ContinuousState.Paused) return;
            State = ContinuousState.Active;
            OnResumed();
        }

        public void End(ContinuousEndReason reason)
        {
            if (IsTerminated) return;
            OnEnding(reason);
            CompleteEnd(reason);
        }

        public void Abort(string reason)
        {
            End(ContinuousEndReason.Interrupted);
        }

        protected void AdvanceElapsed(float deltaTimeSeconds)
        {
            AdvanceElapsedRaw(DeterministicMathBridge.ToFixed(deltaTimeSeconds).RawValue);
        }

        protected internal void AdvanceElapsedRaw(long deltaTimeRaw)
        {
            if (deltaTimeRaw > 0L)
            {
                _elapsedRaw += deltaTimeRaw;
            }
        }

        /// <summary>直接设置累计 raw（回滚/恢复路径，绕过加法）。</summary>
        protected internal void SetElapsedRaw(long elapsedRaw)
        {
            _elapsedRaw = elapsedRaw;
        }

        protected void ResetElapsed()
        {
            _elapsedRaw = 0L;
        }

        protected virtual bool OnActivating() => true;
        protected virtual void OnActivated() { }
        protected virtual void OnPaused() { }
        protected virtual void OnResumed() { }
        protected virtual void OnEnding(ContinuousEndReason reason) { }

        private void CompleteEnd(ContinuousEndReason reason)
        {
            if (IsTerminated) return;
            State = reason == ContinuousEndReason.Completed ? ContinuousState.Expired : ContinuousState.Aborted;
            OnEnded?.Invoke(this, reason);
        }
    }
}
