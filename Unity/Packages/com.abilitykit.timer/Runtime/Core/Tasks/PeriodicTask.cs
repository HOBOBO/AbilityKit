using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 周期任务。
    /// 以固定间隔重复执行回调。
    /// </summary>
    public sealed class PeriodicTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _period;
        private readonly float _duration;
        private readonly int _maxExecutions;
        private float _elapsed;
        private int _executionCount;

        public override float ElapsedTime => _elapsed;
        public override float Duration => _duration < 0 ? float.MaxValue : _duration;

        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) return TaskState.Completed;
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
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) return true;
                if (_duration > 0 && _elapsed >= _duration) return true;
                return false;
            }
        }

        /// <summary>当前执行次数</summary>
        public int ExecutionCount => _executionCount;

        /// <summary>
        /// 构造周期任务
        /// </summary>
        /// <param name="callback">周期执行的回调</param>
        /// <param name="periodSeconds">周期间隔（秒）</param>
        /// <param name="durationSeconds">持续时间（秒），-1 表示无限</param>
        /// <param name="maxExecutions">最大执行次数，-1 表示无限</param>
        public PeriodicTask(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1)
        {
            _callback = callback;
            _period = periodSeconds;
            _duration = durationSeconds;
            _maxExecutions = maxExecutions;
            _elapsed = 0f;
            _executionCount = 0;
        }

        public override void Update(float deltaTime)
        {
            if (IsCompleted || _canceled) return;

            _elapsed += deltaTime;

            while (_elapsed >= _period)
            {
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) break;
                if (_duration > 0 && _elapsed - _period >= _duration) break;

                _elapsed -= _period;
                _callback?.Invoke();
                _executionCount++;
            }
        }
    }
}