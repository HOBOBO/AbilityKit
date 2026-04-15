using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 定时器实例
    /// 整合 ScheduledInstance、TimerInstance、ContinuousTriggerInstance 的功能
    /// </summary>
    public class TimedInstance
    {
        public int InstanceId { get; set; }
        public int TriggerId { get; set; }
        public float DelayMs { get; set; }
        public float IntervalMs { get; set; }
        public int MaxExecutions { get; set; }
        public bool CanBeInterrupted { get; set; }
        public bool IsPaused { get; set; }
        public float ElapsedMs { get; set; }
        public float LastExecuteMs { get; set; }
        public int ExecutionCount { get; set; }
        public bool IsActive { get; set; } = true;
        public string InterruptReason { get; set; }
        public object UserData { get; set; }

        // 委托
        public Action<object, ITriggerDispatcherContext> OnExecute { get; set; }
        public TriggerPredicate<object> OnEvaluate { get; set; }

        /// <summary>
        /// 是否是周期执行（无限或多次）
        /// </summary>
        public bool IsPeriodic => MaxExecutions < 0 || MaxExecutions > 1;

        /// <summary>
        /// 是否达到最大执行次数
        /// </summary>
        public bool IsMaxExecutionsReached => MaxExecutions > 0 && ExecutionCount >= MaxExecutions;
    }

    /// <summary>
    /// 定时调度器
    /// 整合 ScheduledDispatcher + TimeDriver + Continuous 功能
    /// 支持：一次性延迟、周期性执行、持续行为
    /// </summary>
    public class TimedDispatcher : TriggerDispatcherBase
    {
        private readonly List<TimedInstance> _instances = new List<TimedInstance>();
        private readonly List<TimedInstance> _toRemove = new List<TimedInstance>();
        private int _nextInstanceId;

        public override EDispatcherType DispatcherType => EDispatcherType.Timed;
        public override int RegisteredCount => _instances.Count;

        /// <summary>
        /// 每帧执行模式（intervalMs=0 时每帧执行）
        /// </summary>
        public bool FrameUpdateMode { get; set; } = true;

        public TimedDispatcher()
        {
            Name = "TimedDispatcher";
            Priority = 100;
        }

        public override void Initialize()
        {
            _instances.Clear();
            _toRemove.Clear();
            _nextInstanceId = 0;
            _registrations.Clear();
        }

        public override void Dispose()
        {
            _instances.Clear();
            _toRemove.Clear();
            _registrations.Clear();
        }

        public override void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            var instance = new TimedInstance
            {
                InstanceId = _nextInstanceId++,
                TriggerId = plan.TriggerId,
                DelayMs = plan.Schedule.IntervalMs,
                IntervalMs = plan.Schedule.IntervalMs,
                MaxExecutions = plan.Schedule.MaxExecutions,
                CanBeInterrupted = plan.Schedule.CanBeInterrupted,
                OnExecute = (obj, ctx) => executor((TArgs)obj, ctx),
                OnEvaluate = predicate != null ? (pred, ctx) => predicate((TArgs)pred, ctx) : null
            };

            _instances.Add(instance);
            _registrations[plan.TriggerId] = instance;
        }

        public override bool Unregister(int triggerId)
        {
            foreach (var inst in _instances)
            {
                if (inst.TriggerId == triggerId)
                {
                    inst.IsActive = false;
                    _toRemove.Add(inst);
                }
            }
            return _registrations.Remove(triggerId);
        }

        public override void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            if (!IsEnabled) return;

            _toRemove.Clear();

            foreach (var inst in _instances)
            {
                if (!inst.IsActive || inst.IsPaused) continue;

                inst.ElapsedMs += deltaTimeMs;

                // 检查是否需要执行
                bool shouldExecute = false;

                // 首次执行需要等待延迟
                if (inst.ExecutionCount == 0)
                {
                    if (inst.DelayMs > 0)
                    {
                        if (inst.ElapsedMs >= inst.DelayMs)
                        {
                            shouldExecute = true;
                        }
                    }
                    else
                    {
                        shouldExecute = CanExecute(inst);
                    }
                }
                else if (inst.IntervalMs > 0)
                {
                    // 周期性执行
                    float timeSinceLastExecute = inst.ElapsedMs - inst.LastExecuteMs;
                    if (timeSinceLastExecute >= inst.IntervalMs)
                    {
                        shouldExecute = CanExecute(inst);
                    }
                }

                if (shouldExecute)
                {
                    // 评估条件（如果有）
                    if (inst.OnEvaluate != null && !inst.OnEvaluate(null, context))
                    {
                        continue;
                    }

                    // 执行
                    inst.OnExecute?.Invoke(null, context);
                    inst.LastExecuteMs = inst.ElapsedMs;
                    inst.ExecutionCount++;

                    // 检查是否达到最大执行次数
                    if (inst.IsMaxExecutionsReached)
                    {
                        inst.IsActive = false;
                        _toRemove.Add(inst);
                    }
                }
            }

            // 移除已终止的实例
            foreach (var inst in _toRemove)
            {
                _instances.Remove(inst);
            }
        }

        private bool CanExecute(TimedInstance inst)
        {
            if (FrameUpdateMode || inst.IntervalMs <= 0)
            {
                // 每帧执行模式
                return true;
            }

            // 间隔执行模式
            float timeSinceLastExecute = inst.ElapsedMs - inst.LastExecuteMs;
            return timeSinceLastExecute >= inst.IntervalMs;
        }

        /// <summary>
        /// 注册一次性延迟定时器
        /// </summary>
        public int RegisterDelayedExecutor(int triggerId, float delayMs, Action<object, ITriggerDispatcherContext> executor, TriggerPredicate<object> predicate = null)
        {
            var instance = new TimedInstance
            {
                InstanceId = _nextInstanceId++,
                TriggerId = triggerId,
                DelayMs = delayMs,
                IntervalMs = 0,
                MaxExecutions = 1,
                CanBeInterrupted = true,
                OnExecute = executor,
                OnEvaluate = predicate
            };

            _instances.Add(instance);
            _registrations[triggerId] = instance;
            return instance.InstanceId;
        }

        /// <summary>
        /// 注册周期性定时器
        /// </summary>
        public int RegisterPeriodicExecutor(int triggerId, float intervalMs, int maxExecutions = -1, Action<object, ITriggerDispatcherContext> executor = null, TriggerPredicate<object> predicate = null)
        {
            var instance = new TimedInstance
            {
                InstanceId = _nextInstanceId++,
                TriggerId = triggerId,
                DelayMs = intervalMs,
                IntervalMs = intervalMs,
                MaxExecutions = maxExecutions,
                CanBeInterrupted = true,
                OnExecute = executor,
                OnEvaluate = predicate
            };

            _instances.Add(instance);
            _registrations[triggerId] = instance;
            return instance.InstanceId;
        }

        /// <summary>
        /// 中断指定实例
        /// </summary>
        public bool InterruptInstance(int instanceId, string reason)
        {
            foreach (var inst in _instances)
            {
                if (inst.InstanceId == instanceId && inst.CanBeInterrupted)
                {
                    inst.IsActive = false;
                    inst.InterruptReason = reason;
                    _toRemove.Add(inst);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 中断所有指定触发器的实例
        /// </summary>
        public int InterruptAll(int triggerId, string reason)
        {
            int count = 0;
            foreach (var inst in _instances)
            {
                if (inst.TriggerId == triggerId && inst.CanBeInterrupted)
                {
                    inst.IsActive = false;
                    inst.InterruptReason = reason;
                    _toRemove.Add(inst);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 暂停指定实例
        /// </summary>
        public bool PauseInstance(int instanceId)
        {
            foreach (var inst in _instances)
            {
                if (inst.InstanceId == instanceId && inst.IsActive && !inst.IsPaused)
                {
                    inst.IsPaused = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 恢复指定实例
        /// </summary>
        public bool ResumeInstance(int instanceId)
        {
            foreach (var inst in _instances)
            {
                if (inst.InstanceId == instanceId && inst.IsPaused)
                {
                    inst.IsPaused = false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取指定触发器的活跃实例数量
        /// </summary>
        public int GetActiveCount(int triggerId)
        {
            int count = 0;
            foreach (var inst in _instances)
            {
                if (inst.TriggerId == triggerId && inst.IsActive)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取实例
        /// </summary>
        public TimedInstance GetInstance(int instanceId)
        {
            foreach (var inst in _instances)
            {
                if (inst.InstanceId == instanceId)
                    return inst;
            }
            return null;
        }

        /// <summary>
        /// 获取所有活跃实例
        /// </summary>
        public IEnumerable<TimedInstance> GetActiveInstances()
        {
            foreach (var inst in _instances)
            {
                if (inst.IsActive)
                    yield return inst;
            }
        }
    }
}