using System;
using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 持续运行时实现共享的状态机基类。
    /// </summary>
    public abstract class MobaContinuousRuntimeBase : IContinuous
    {
        public abstract IContinuousConfig Config { get; }

        public ContinuousState State { get; private set; } = ContinuousState.Inactive;
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;
        public float ElapsedSeconds { get; private set; }

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
            if (deltaTimeSeconds > 0f)
            {
                ElapsedSeconds += deltaTimeSeconds;
            }
        }

        protected void ResetElapsed()
        {
            ElapsedSeconds = 0f;
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
