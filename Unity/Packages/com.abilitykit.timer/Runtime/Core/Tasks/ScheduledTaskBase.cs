using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 调度任务基类。
    /// 提供公共的状态管理和取消机制。
    /// </summary>
    public abstract class ScheduledTaskBase : IScheduledTask
    {
        public virtual string Name { get; set; }
        public string CancelReason { get; protected set; }
        public float StartTime { get; set; }

        protected bool _canceled;
        protected bool _completed;

        public abstract TaskState State { get; }
        public abstract bool IsCompleted { get; }
        public abstract float ElapsedTime { get; }
        public abstract float Duration { get; }

        public bool IsCanceled => _canceled;
        public bool IsTimedOut => State == TaskState.TimedOut;

        public virtual void RequestCancel(string reason = null)
        {
            _canceled = true;
            CancelReason = reason;
        }

        public virtual void Complete()
        {
            _completed = true;
        }

        public abstract void Update(float deltaTime);
    }
}
