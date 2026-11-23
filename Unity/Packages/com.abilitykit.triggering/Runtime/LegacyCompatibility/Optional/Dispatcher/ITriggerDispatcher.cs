using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 旧派发器登记计划时使用的执行委托。
    /// </summary>
    public delegate void TriggerExecutor<TArgs>(TArgs args, ITriggerDispatcherContext context)
        where TArgs : class;

    /// <summary>
    /// 标识旧派发器的驱动类型，不规定正式调度链的执行顺序。
    /// </summary>
    public enum EDispatcherType
    {
        /// <summary>
        /// 事件总线触发。
        /// </summary>
        Event = 0,

        /// <summary>
        /// 延迟或周期触发。
        /// </summary>
        Timed = 1,

        /// <summary>
        /// 由管线阶段驱动。
        /// </summary>
        Phase = 2,

        /// <summary>
        /// 由增益或减益系统驱动。
        /// </summary>
        Buff = 3,
    }

    /// <summary>
    /// 旧派发器的计划注册与更新契约，仅为现有调用方保留。
    /// 正式事件订阅和动作调度不要求实现此接口。
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
    /// 保存旧派发器的一条计划及其条件和执行委托。
    /// </summary>
    public readonly struct DispatcherRegistration<TArgs>
        where TArgs : class
    {
        /// <summary>
        /// 旧派发器用于索引此登记项的触发器 ID。
        /// </summary>
        public readonly int TriggerId;

        /// <summary>
        /// 登记的计划。
        /// </summary>
        public readonly TriggerPlan<TArgs> Plan;

        /// <summary>
        /// 登记的条件委托。
        /// </summary>
        public readonly TriggerPredicate<TArgs> Predicate;

        /// <summary>
        /// 登记的执行委托。
        /// </summary>
        public readonly TriggerExecutor<TArgs> Executor;

        /// <summary>
        /// 保存调用方提供的 ID、计划与委托，不执行登记或校验。
        /// </summary>
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
    /// 为旧派发器提供属性和按 ID 保存登记项的基础实现。
    /// </summary>
    public abstract class TriggerDispatcherBase : ITriggerDispatcher
    {
        /// <inheritdoc />
        public abstract EDispatcherType DispatcherType { get; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public bool IsEnabled { get; set; } = true;

        /// <inheritdoc />
        public int Priority { get; set; }

        /// <inheritdoc />
        public abstract int RegisteredCount { get; }

        /// <summary>
        /// 供派生实现管理的登记项，按触发器 ID 索引。
        /// </summary>
        protected readonly Dictionary<int, object> _registrations = new Dictionary<int, object>();

        /// <inheritdoc />
        public abstract void Initialize();

        /// <inheritdoc />
        public abstract void Dispose();

        /// <inheritdoc />
        public abstract void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor) where TArgs : class;

        /// <inheritdoc />
        public abstract bool Unregister(int triggerId);

        /// <inheritdoc />
        public abstract void Update(float deltaTimeMs, ITriggerDispatcherContext context);

        /// <summary>
        /// 读取指定 ID 的登记项；未登记时返回默认值。
        /// </summary>
        /// <exception cref="System.InvalidCastException">登记项的类型与请求的类型不匹配。</exception>
        protected DispatcherRegistration<TArgs> GetRegistration<TArgs>(int triggerId) where TArgs : class
        {
            if (_registrations.TryGetValue(triggerId, out var obj))
                return (DispatcherRegistration<TArgs>)obj;
            return default;
        }
    }

}
