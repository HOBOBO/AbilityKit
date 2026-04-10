using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 默认调度器。
    /// 管理一组调度任务的执行。
    ///
    /// 使用示例：
    /// ```csharp
    /// var scheduler = new DefaultScheduler();
    ///
    /// // 延时 2 秒执行
    /// scheduler.ScheduleDelay(() => Debug.Log("2秒后"), 2f);
    ///
    /// // 每秒执行一次，持续 10 秒
    /// scheduler.SchedulePeriodic(() => Debug.Log("tick"), 1f, 10f);
    ///
    /// // 每帧更新
    /// void Update() => scheduler.Tick(Time.deltaTime);
    /// ```
    /// </summary>
    public class DefaultScheduler : IScheduler
    {
        private readonly TaskList _tasks = new(16);

        public string Name { get; set; } = "DefaultScheduler";
        public int Count => _tasks.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask ScheduleDelay(Action callback, float delaySeconds)
        {
            var task = new DelayTask(callback, delaySeconds);
            _tasks.Add(task);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask SchedulePeriodic(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1)
        {
            var task = new PeriodicTask(callback, periodSeconds, durationSeconds, maxExecutions);
            _tasks.Add(task);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IScheduledTask ScheduleContinuous(Action<float> onTick, Action onComplete = null, float durationSeconds = -1)
        {
            var task = new ContinuousTask(onTick, onComplete, durationSeconds);
            _tasks.Add(task);
            return task;
        }

        public void CancelAll()
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                _tasks[i].RequestCancel("Canceled by CancelAll");
            }
        }

        public void CancelByName(string name)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Name == name)
                    _tasks[i].RequestCancel($"Canceled by name: {name}");
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                task.Update(deltaTime);

                if (task.IsCompleted || task.IsCanceled)
                {
                    _tasks.RemoveAt(i);
                }
            }
        }
    }
}
