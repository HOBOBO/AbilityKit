using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 触发器调度器注册中心
    /// 统一管理所有类型的调度器
    /// </summary>
    public class TriggerDispatcherRegistry
    {
        private readonly List<ITriggerDispatcher> _dispatchers = new List<ITriggerDispatcher>();
        private readonly Dictionary<EDispatcherType, ITriggerDispatcher> _dispatchersByType = new Dictionary<EDispatcherType, ITriggerDispatcher>();
        private readonly Dictionary<Type, ITriggerDispatcher> _dispatchersByContextType = new Dictionary<Type, ITriggerDispatcher>();

        /// <summary>
        /// 已注册的调度器数量
        /// </summary>
        public int DispatcherCount => _dispatchers.Count;

        /// <summary>
        /// 总注册的触发器数量
        /// </summary>
        public int TotalRegisteredCount
        {
            get
            {
                int count = 0;
                foreach (var d in _dispatchers)
                {
                    count += d.RegisteredCount;
                }
                return count;
            }
        }

        /// <summary>
        /// 初始化注册中心
        /// </summary>
        public void Initialize()
        {
            // 创建默认调度器
            var eventBusDispatcher = new EventBusDispatcher(new Eventing.EventBus());
            Register(eventBusDispatcher);

            var timedDispatcher = new TimedDispatcher();
            Register(timedDispatcher);

            // 注意：PhaseDispatcher 和 BuffDispatcher 已移至业务层
            // 如需使用，请从业务包引用 MobaPhaseDispatcher / MobaBuffDispatcher
        }

        /// <summary>
        /// 注册调度器
        /// </summary>
        public void Register(ITriggerDispatcher dispatcher)
        {
            if (dispatcher == null) return;

            dispatcher.Initialize();
            _dispatchers.Add(dispatcher);
            _dispatchersByType[dispatcher.DispatcherType] = dispatcher;

            // 排序：按优先级
            _dispatchers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 注销调度器
        /// </summary>
        public bool Unregister(EDispatcherType type)
        {
            if (_dispatchersByType.TryGetValue(type, out var dispatcher))
            {
                dispatcher.Dispose();
                _dispatchers.Remove(dispatcher);
                _dispatchersByType.Remove(type);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取指定类型的调度器
        /// </summary>
        public T GetDispatcher<T>() where T : class, ITriggerDispatcher
        {
            foreach (var d in _dispatchers)
            {
                if (d is T t) return t;
            }
            return null;
        }

        /// <summary>
        /// 获取指定类型的调度器
        /// </summary>
        public ITriggerDispatcher GetDispatcher(EDispatcherType type)
        {
            return _dispatchersByType.TryGetValue(type, out var d) ? d : null;
        }

        /// <summary>
        /// 获取事件总线调度器
        /// </summary>
        public EventBusDispatcher Event => GetDispatcher<EventBusDispatcher>();

        /// <summary>
        /// 获取定时调度器
        /// </summary>
        public TimedDispatcher Timed => GetDispatcher<TimedDispatcher>();

        /// <summary>
        /// 更新所有需要每帧更新的调度器
        /// </summary>
        public void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            foreach (var dispatcher in _dispatchers)
            {
                if (dispatcher.IsEnabled)
                {
                    dispatcher.Update(deltaTimeMs, context);
                }
            }
        }

        /// <summary>
        /// 注册触发器到指定类型的调度器
        /// </summary>
        public void RegisterTo<TArgs, TDispatcher>(int triggerId, in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
            where TDispatcher : class, ITriggerDispatcher
        {
            var dispatcher = GetDispatcher<TDispatcher>();
            dispatcher?.Register(in plan, predicate, executor);
        }

        /// <summary>
        /// 注册触发器到所有适用的调度器
        /// 根据 Schedule.Mode 自动选择调度器
        /// </summary>
        public void RegisterToAll<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            // 根据调度模式选择调度器
            switch (plan.Schedule.Mode)
            {
                case Config.EScheduleMode.Timed:
                    Timed.Register(in plan, predicate, executor);
                    break;

                case Config.EScheduleMode.Periodic:
                    Timed.Register(in plan, predicate, executor);
                    break;

                case Config.EScheduleMode.Continuous:
                    Timed.Register(in plan, predicate, executor);
                    break;

                case Config.EScheduleMode.Conditional:
                    // 条件触发：原 PhaseDispatcher 已移至业务层
                    // 如需使用，请从业务包手动注册到 MobaPhaseDispatcher
                    Timed.Register(in plan, predicate, executor);
                    break;

                case Config.EScheduleMode.External:
                    // 外部控制：原 BuffDispatcher 已移至业务层
                    // 如需使用，请从业务包手动注册到 MobaBuffDispatcher
                    Timed.Register(in plan, predicate, executor);
                    break;

                case Config.EScheduleMode.Transient:
                default:
                    Event.Register(in plan, predicate, executor);
                    break;
            }
        }

        /// <summary>
        /// 销毁注册中心
        /// </summary>
        public void Dispose()
        {
            foreach (var dispatcher in _dispatchers)
            {
                dispatcher.Dispose();
            }
            _dispatchers.Clear();
            _dispatchersByType.Clear();
            _dispatchersByContextType.Clear();
        }

        /// <summary>
        /// 获取所有调度器
        /// </summary>
        public IEnumerable<ITriggerDispatcher> GetAllDispatchers()
        {
            return _dispatchers;
        }
    }

    /// <summary>
    /// 触发器调度器特性
    /// 用于标记调度器类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DispatcherAttribute : Attribute
    {
        public EDispatcherType Type { get; }
        public string Name { get; }
        public int Priority { get; }

        public DispatcherAttribute(EDispatcherType type, string name = null, int priority = 100)
        {
            Type = type;
            Name = name;
            Priority = priority;
        }
    }
}