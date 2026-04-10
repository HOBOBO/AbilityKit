using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 持续任务。
    /// 持续执行回调直到外部终止或超时。
    /// </summary>
    public sealed class ContinuousTask : ScheduledTaskBase
    {
        private readonly Action<float> _onTick;
        private readonly Action _onComplete;
        private readonly float _duration;
        private float _elapsed;

        public override float ElapsedTime => _elapsed;
        public override float Duration => _duration < 0 ? float.MaxValue : _duration;

        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_duration > 0 && _elapsed >= _duration) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        public override bool IsCompleted
        {
            get
            {
                if (_canceled) return true;
                if (_completed) return true;
                if (_duration > 0 && _elapsed >= _duration) return true;
                return false;
            }
        }

        /// <summary>
        /// 构造持续任务
        /// </summary>
        /// <param name="onTick">每帧/每个周期执行的回调，参数为 deltaTime</param>
        /// <param name="onComplete">任务完成时的回调（可为空）</param>
        /// <param name="durationSeconds">持续时间（秒），-1 表示无限</param>
        public ContinuousTask(Action<float> onTick, Action onComplete, float durationSeconds = -1)
        {
            _onTick = onTick;
            _onComplete = onComplete;
            _duration = durationSeconds;
            _elapsed = 0f;
        }

        public override void Update(float deltaTime)
        {
            if (IsCompleted || _canceled) return;

            _elapsed += deltaTime;
            _onTick?.Invoke(deltaTime);

            if (_duration > 0 && _elapsed >= _duration)
            {
                _onComplete?.Invoke();
                _completed = true;
            }
        }
    }
}