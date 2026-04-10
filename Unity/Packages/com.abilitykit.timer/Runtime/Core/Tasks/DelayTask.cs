using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 延时任务。
    /// 在指定延迟后执行一次回调。
    /// </summary>
    public sealed class DelayTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _delay;
        private float _elapsed;

        public override float ElapsedTime => _elapsed;
        public override float Duration => _delay;

        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_elapsed >= _delay) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        public override bool IsCompleted => _elapsed >= _delay || _completed;

        /// <summary>
        /// 构造延时任务
        /// </summary>
        /// <param name="callback">到期执行的回调</param>
        /// <param name="delaySeconds">延迟时间（秒）</param>
        public DelayTask(Action callback, float delaySeconds)
        {
            _callback = callback;
            _delay = delaySeconds;
            _elapsed = 0f;
        }

        public override void Update(float deltaTime)
        {
            if (IsCompleted || _canceled) return;

            _elapsed += deltaTime;

            if (_elapsed >= _delay)
            {
                _callback?.Invoke();
                _completed = true;
            }
        }
    }
}
