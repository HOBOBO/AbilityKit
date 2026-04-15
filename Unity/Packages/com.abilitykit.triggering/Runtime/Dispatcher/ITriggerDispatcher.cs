using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 触发器条件委托
    /// </summary>
    public delegate bool TriggerPredicate<TArgs>(TArgs args, ITriggerDispatcherContext context)
        where TArgs : class;

    /// <summary>
    /// 触发器执行委托
    /// </summary>
    public delegate void TriggerExecutor<TArgs>(TArgs args, ITriggerDispatcherContext context)
        where TArgs : class;
    /// <summary>
    /// 触发器调度器类型枚举
    /// </summary>
    public enum EDispatcherType
    {
        /// <summary>事件总线触发（EventBus Publish/Subscribe）</summary>
        Event = 0,
        /// <summary>定时调度（延迟/周期执行）</summary>
        Timed = 1,
        /// <summary>管线触发（Pipeline Phase 驱动）</summary>
        Phase = 2,
        /// <summary>Buff触发（由Buff系统驱动）</summary>
        Buff = 3,
    }

    /// <summary>
    /// 触发器调度器接口
    /// 统一抽象不同类型的触发器驱动方式
    /// </summary>
    public interface ITriggerDispatcher
    {
        /// <summary>
        /// 调度器类型
        /// </summary>
        EDispatcherType DispatcherType { get; }

        /// <summary>
        /// 调度器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 优先级（数值越小越优先）
        /// </summary>
        int Priority { get; set; }

        /// <summary>
        /// 初始化调度器
        /// </summary>
        void Initialize();

        /// <summary>
        /// 销毁调度器
        /// </summary>
        void Dispose();

        /// <summary>
        /// 注册触发器计划
        /// </summary>
        void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class;

        /// <summary>
        /// 注销触发器计划
        /// </summary>
        bool Unregister(int triggerId);

        /// <summary>
        /// 每帧更新（部分调度器类型需要）
        /// </summary>
        void Update(float deltaTimeMs, ITriggerDispatcherContext context);

        /// <summary>
        /// 获取已注册的触发器数量
        /// </summary>
        int RegisteredCount { get; }
    }

    /// <summary>
    /// 触发器调度器上下文接口
    /// 用于传递给调度器的执行上下文
    /// </summary>
    public interface ITriggerDispatcherContext
    {
        /// <summary>
        /// 关联的执行器/世界解析器
        /// </summary>
        object Context { get; }

        /// <summary>
        /// 尝试获取指定类型的服务
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// 获取当前时间（毫秒）
        /// </summary>
        float CurrentTimeMs { get; }
    }

    /// <summary>
    /// 触发器调度器注册信息
    /// </summary>
    public readonly struct DispatcherRegistration<TArgs>
        where TArgs : class
    {
        public readonly int TriggerId;
        public readonly TriggerPlan<TArgs> Plan;
        public readonly TriggerPredicate<TArgs> Predicate;
        public readonly TriggerExecutor<TArgs> Executor;

        public DispatcherRegistration(
            int triggerId,
            in TriggerPlan<TArgs> plan,
            TriggerPredicate<TArgs> predicate,
            TriggerExecutor<TArgs> executor)
        {
            TriggerId = triggerId;
            Plan = plan;
            Predicate = predicate;
            Executor = executor;
        }
    }

    /// <summary>
    /// 调度器基类
    /// </summary>
    public abstract class TriggerDispatcherBase : ITriggerDispatcher
    {
        public abstract EDispatcherType DispatcherType { get; }
        public string Name { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }
        public abstract int RegisteredCount { get; }

        protected readonly Dictionary<int, object> _registrations = new Dictionary<int, object>();

        public abstract void Initialize();
        public abstract void Dispose();
        public abstract void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor) where TArgs : class;
        public abstract bool Unregister(int triggerId);
        public abstract void Update(float deltaTimeMs, ITriggerDispatcherContext context);

        protected DispatcherRegistration<TArgs> GetRegistration<TArgs>(int triggerId) where TArgs : class
        {
            if (_registrations.TryGetValue(triggerId, out var obj))
                return (DispatcherRegistration<TArgs>)obj;
            return default;
        }
    }

    /// <summary>
    /// 事件总线触发器上下文
    /// </summary>
    public class EventBusDispatcherContext : ITriggerDispatcherContext
    {
        public object Context { get; }
        public float CurrentTimeMs { get; }

        public EventBusDispatcherContext(object context, float currentTimeMs = 0)
        {
            Context = context;
            CurrentTimeMs = currentTimeMs;
        }

        public T GetService<T>() where T : class
        {
            return Context as T;
        }
    }
}
