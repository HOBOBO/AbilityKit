using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// Buff 触发事件类型
    /// </summary>
    public enum EBuffTriggerEvent
    {
        /// <summary>Buff 开始</summary>
        OnStart = 0,
        /// <summary>Buff 结束</summary>
        OnEnd = 1,
        /// <summary>Buff 周期触发（DOT/HOT 等）</summary>
        OnTick = 2,
        /// <summary>Buff 被驱散</summary>
        OnDispel = 3,
        /// <summary>Buff 被覆盖</summary>
        OnStackChange = 4,
        /// <summary>Buff 刷新</summary>
        OnRefresh = 5,
    }

    /// <summary>
    /// Buff 触发器注册信息
    /// </summary>
    internal class BuffTriggerRegistration
    {
        public int TriggerId { get; set; }
        public TriggerPredicate<object> Predicate { get; set; }
        public TriggerExecutor<object> Executor { get; set; }
    }

    /// <summary>
    /// Buff 调度器
    /// 整合 BuffDriver 功能
    /// 由 Buff 系统驱动触发器
    /// </summary>
    public class BuffDispatcher : ITriggerDispatcher
    {
        private readonly Dictionary<int, Dictionary<EBuffTriggerEvent, List<BuffTriggerRegistration>>> _buffRegistrations =
            new Dictionary<int, Dictionary<EBuffTriggerEvent, List<BuffTriggerRegistration>>>();

        private readonly Dictionary<int, object> _registrations = new Dictionary<int, object>();

        public EDispatcherType DispatcherType => EDispatcherType.Buff;
        public string Name { get; set; } = "BuffDispatcher";
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; } = 60;
        public int RegisteredCount => _registrations.Count;

        public void Initialize()
        {
            _buffRegistrations.Clear();
            _registrations.Clear();
        }

        public void Dispose()
        {
            _buffRegistrations.Clear();
            _registrations.Clear();
        }

        public void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            var registration = new BuffTriggerRegistration
            {
                TriggerId = plan.TriggerId,
                Predicate = predicate != null ? (pred, ctx) => predicate((TArgs)pred, ctx) : null,
                Executor = (obj, ctx) => executor((TArgs)obj, ctx)
            };

            _registrations[plan.TriggerId] = registration;
        }

        public bool Unregister(int triggerId)
        {
            return _registrations.Remove(triggerId);
        }

        public void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            // BuffDispatcher 由 Buff 系统通过 OnBuffEvent 调用
        }

        /// <summary>
        /// 注册 Buff 触发器
        /// </summary>
        public void RegisterBuffTrigger(int buffId, EBuffTriggerEvent triggerEvent, int triggerId)
        {
            if (!_buffRegistrations.TryGetValue(buffId, out var events))
            {
                events = new Dictionary<EBuffTriggerEvent, List<BuffTriggerRegistration>>();
                _buffRegistrations[buffId] = events;
            }

            if (!events.TryGetValue(triggerEvent, out var list))
            {
                list = new List<BuffTriggerRegistration>();
                events[triggerEvent] = list;
            }

            if (_registrations.TryGetValue(triggerId, out var obj) && obj is BuffTriggerRegistration reg)
            {
                if (!list.Contains(reg))
                {
                    list.Add(reg);
                }
            }
        }

        /// <summary>
        /// 触发 Buff 事件
        /// 由 Buff 系统调用
        /// </summary>
        public void OnBuffEvent(int buffId, EBuffTriggerEvent triggerEvent, object args, ITriggerDispatcherContext context)
        {
            if (!IsEnabled) return;

            if (_buffRegistrations.TryGetValue(buffId, out var events))
            {
                if (events.TryGetValue(triggerEvent, out var list))
                {
                    foreach (var reg in list)
                    {
                        if (reg.Predicate != null && !reg.Predicate(args, context))
                        {
                            continue;
                        }

                        reg.Executor(args, context);
                    }
                }
            }
        }

        /// <summary>
        /// 触发所有事件的 Buff 事件
        /// </summary>
        public void OnBuffAllEvents(int buffId, object args, ITriggerDispatcherContext context)
        {
            if (!IsEnabled) return;

            if (_buffRegistrations.TryGetValue(buffId, out var events))
            {
                foreach (var eventList in events.Values)
                {
                    foreach (var reg in eventList)
                    {
                        if (reg.Predicate != null && !reg.Predicate(args, context))
                        {
                            continue;
                        }

                        reg.Executor(args, context);
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定 Buff 的触发器数量
        /// </summary>
        public int GetTriggerCount(int buffId)
        {
            if (!_buffRegistrations.TryGetValue(buffId, out var events))
            {
                return 0;
            }

            int count = 0;
            foreach (var list in events.Values)
            {
                count += list.Count;
            }
            return count;
        }

        /// <summary>
        /// 移除指定 Buff 的所有触发器
        /// </summary>
        public void RemoveBuffTriggers(int buffId)
        {
            _buffRegistrations.Remove(buffId);
        }
    }
}