using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 延时任务。
    /// 在指定延迟后执行一次回调。
    ///
    /// 确定性说明：内部用 Fixed64 raw 累加（对外 float 无感），避免 float 多次累加的漂移；
    /// 边界处单次 IEEE 换算（跨平台位一致）。
    /// </summary>
    public sealed class DelayTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _delay;
        private readonly Fixed64 _delayRaw;
        private Fixed64 _elapsedRaw;

        public override float ElapsedTime => DeterministicMathBridge.ToSingle(_elapsedRaw);
        public override float Duration => _delay;

        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_elapsedRaw >= _delayRaw) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        public override bool IsCompleted => _elapsedRaw >= _delayRaw || _completed;

        /// <summary>
        /// 构造延时任务
        /// </summary>
        /// <param name="callback">到期执行的回调</param>
        /// <param name="delaySeconds">延迟时间（秒）</param>
        public DelayTask(Action callback, float delaySeconds)
        {
            _callback = callback;
            _delay = delaySeconds;
            _delayRaw = DeterministicMathBridge.ToFixed(delaySeconds);
            _elapsedRaw = Fixed64.Zero;
        }

        public override void Update(float deltaTime)
        {
            // 不能用 IsCompleted 做守卫：零/负延迟任务在构造后即为 IsCompleted，
            // 会导致回调被永久吞掉。只检查“已触发完成”与“已取消”。
            if (_completed || _canceled) return;

            _elapsedRaw += DeterministicMathBridge.ToFixed(deltaTime);

            if (_elapsedRaw >= _delayRaw)
            {
                _callback?.Invoke();
                _completed = true;
            }
        }
    }
}
