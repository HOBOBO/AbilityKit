using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 由单一调用方串行推进的调度器。回调可添加或停止任务，但不能递归推进本实例。
    /// </summary>
    public class DefaultScheduler : IScheduler
    {
        private readonly TaskList _tasks = new(16);
        private bool _isTicking;

        /// <summary>
        /// 获取或设置用于标识此调度器的名称。
        /// </summary>
        public string Name { get; set; } = "DefaultScheduler";

        /// <summary>
        /// 获取仍在调度队列中的任务数，包含尚未被清理的取消或完成任务。
        /// </summary>
        public int Count => _tasks.Count;

        /// <summary>
        /// 加入一个以秒为单位计时的单次延时任务。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask ScheduleDelay(Action callback, float delaySeconds)
        {
            var task = new DelayTask(callback, delaySeconds);
            _tasks.Add(task);
            return task;
        }

        /// <summary>
        /// 加入一个按秒间隔触发的周期任务，可指定截止时间或次数上限。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask SchedulePeriodic(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1)
        {
            var task = new PeriodicTask(callback, periodSeconds, durationSeconds, maxExecutions);
            _tasks.Add(task);
            return task;
        }

        /// <summary>
        /// 加入一个逐次推进的持续任务，可在自然到期时执行完成回调。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask ScheduleContinuous(Action<float> onTick, Action onComplete = null, float durationSeconds = -1)
        {
            var task = new ContinuousTask(onTick, onComplete, durationSeconds);
            _tasks.Add(task);
            return task;
        }

        /// <summary>
        /// 标记当前队列中的任务为取消，不在调用期间移除它们。
        /// </summary>
        public void CancelAll()
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                _tasks[i].RequestCancel("Canceled by CancelAll");
            }
        }

        /// <summary>
        /// 标记名称完全匹配的任务为取消；任务在后续被访问时移除。
        /// </summary>
        public void CancelByName(string name)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Name == name)
                    _tasks[i].RequestCancel($"Canceled by name: {name}");
            }
        }

        /// <summary>
        /// 使用调用方提供的秒增量推进任务；本次回调添加的任务最早在下次推进。
        /// 更新失败时取消并移除失败任务，保留已推进的结果并原样抛出异常。
        /// </summary>
        /// <exception cref="InvalidOperationException">回调递归推进同一调度器。</exception>
        public void Tick(float deltaTime)
        {
            if (_isTicking)
            {
                throw new InvalidOperationException("A scheduler cannot be ticked from its own callback.");
            }

            _isTicking = true;
            try
            {
                /*
                 * 倒序扫描起始队列。移除时调入当前位置的尾项已推进过，或者是回调新加入的任务。
                 */
                for (var index = _tasks.Count - 1; index >= 0; index--)
                {
                    AdvanceTask(index, deltaTime);
                }
            }
            finally
            {
                _isTicking = false;
            }
        }

        private void AdvanceTask(int index, float deltaTime)
        {
            var task = _tasks[index];
            try
            {
                task.Update(deltaTime);
            }
            catch
            {
                if (!task.IsCanceled)
                {
                    task.RequestCancel("Canceled after task update failed");
                }

                _tasks.RemoveAt(index);
                throw;
            }

            if (task.IsCompleted || task.IsCanceled)
            {
                _tasks.RemoveAt(index);
            }
        }
    }
}
