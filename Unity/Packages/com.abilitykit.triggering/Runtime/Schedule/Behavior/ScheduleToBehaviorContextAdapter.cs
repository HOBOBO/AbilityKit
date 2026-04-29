using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Behavior;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度上下文到行为上下文的适配器
    /// 将 IScheduleManager 的 ScheduleContext 适配为 IBehaviorContext
    /// 用于 SchedulableBehaviorScheduleAdapter
    /// </summary>
    public sealed class ScheduleToBehaviorContextAdapter : IBehaviorContext
    {
        private readonly ScheduleContext _scheduleContext;
        private readonly IBehaviorContext _innerContext;
        private readonly ScheduleBlackboard _blackboard;

        public object Args { get; }
        public IBlackboardResolver Blackboards { get; }
        public IActionRegistry Actions => _innerContext?.Actions;
        public IValueResolver Values => _innerContext?.Values;

        /// <summary>
        /// 创建一个新的适配器
        /// </summary>
        /// <param name="scheduleContext">调度上下文</param>
        /// <param name="innerContext">内部行为上下文（提供 Actions、Values 等），可为空</param>
        /// <param name="args">传递给行为的参数</param>
        public ScheduleToBehaviorContextAdapter(
            in ScheduleContext scheduleContext,
            IBehaviorContext innerContext = null,
            object args = null)
        {
            _scheduleContext = scheduleContext;
            _innerContext = innerContext;
            _blackboard = new ScheduleBlackboard(scheduleContext);
            
            // 如果没有提供 Args，使用 InstanceId
            Args = args ?? scheduleContext.InstanceId;
            Blackboards = _blackboard;
        }

        /// <summary>
        /// 获取调度上下文信息
        /// </summary>
        public ScheduleContext ScheduleContext => _scheduleContext;

        /// <summary>
        /// 获取经过速度缩放的时间增量
        /// </summary>
        public float ScaledDeltaTimeMs => _scheduleContext.ScaledDeltaMs;

        /// <summary>
        /// 获取总消耗时间
        /// </summary>
        public float ElapsedMs => _scheduleContext.ElapsedMs;

        /// <summary>
        /// 获取当前执行次数
        /// </summary>
        public int ExecutionCount => _scheduleContext.ExecutionCount;

        /// <summary>
        /// 获取速度倍率
        /// </summary>
        public float Speed => _scheduleContext.Speed;
    }

    /// <summary>
    /// 基于调度上下文的黑板实现
    /// 用于在调度过程中存储临时数据
    /// </summary>
    public sealed class ScheduleBlackboard : IBlackboardResolver
    {
        private readonly Dictionary<(int boardId, string key), object> _storage = new();

        public ScheduleBlackboard(in ScheduleContext context)
        {
            // 可以在此存储初始上下文信息
        }

        public bool TryGetValue<T>(int boardId, string key, out T value)
        {
            var kvp = (boardId, key);
            if (_storage.TryGetValue(kvp, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public void SetValue<T>(int boardId, string key, T value)
        {
            var kvp = (boardId, key);
            if (value == null)
            {
                _storage.Remove(kvp);
            }
            else
            {
                _storage[kvp] = value;
            }
        }
    }
}
