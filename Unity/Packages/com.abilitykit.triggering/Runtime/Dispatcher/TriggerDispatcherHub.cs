using System;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Continuous;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 统一触发器调度入口
    /// 封装 TriggerDispatcherRegistry，提供简洁的 API
    /// </summary>
    public class TriggerDispatcherHub
    {
        private readonly TriggerDispatcherRegistry _registry;
        private ITriggerDispatcherContext _currentContext;

        // ✅ 新增：ActionScheduler 全局管理器
        private readonly ActionSchedulerManager _actionSchedulerManager;

        /// <summary>
        /// 创建调度中心
        /// </summary>
        public TriggerDispatcherHub()
        {
            _registry = new TriggerDispatcherRegistry();
            _actionSchedulerManager = new ActionSchedulerManager();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            _registry.Initialize();
        }

        /// <summary>
        /// 设置当前上下文
        /// </summary>
        public void SetContext(ITriggerDispatcherContext context)
        {
            _currentContext = context;
        }

        /// <summary>
        /// 获取事件总线调度器
        /// </summary>
        public EventBusDispatcher Event => _registry.Event;

        /// <summary>
        /// 获取定时调度器
        /// </summary>
        public TimedDispatcher Timed => _registry.Timed;

        /// <summary>
        /// 获取 Action 调度器管理器
        /// </summary>
        public ActionSchedulerManager ActionSchedulerManager => _actionSchedulerManager;

        /// <summary>
        /// 注册持续行为触发器
        /// 复用一个 Predicate 和 Action 的原子操作
        /// </summary>
        public void RegisterContinuous<TArgs>(
            int triggerId,
            in TriggerPlan<TArgs> plan,
            TriggerPredicate<TArgs> predicate,
            TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            // 持续行为使用 TimedDispatcher
            _registry.Timed.Register(in plan, predicate, executor);
        }

        /// <summary>
        /// 注册事件触发器
        /// </summary>
        public void RegisterEvent<TArgs>(
            int triggerId,
            in TriggerPlan<TArgs> plan,
            TriggerPredicate<TArgs> predicate,
            TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            _registry.Event.Register(in plan, predicate, executor);
        }

        /// <summary>
        /// 注册定时触发器
        /// </summary>
        public void RegisterTimed<TArgs>(
            int triggerId,
            in TriggerPlan<TArgs> plan,
            TriggerPredicate<TArgs> predicate,
            TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            _registry.Timed.Register(in plan, predicate, executor);
        }

        /// <summary>
        /// 注册到自动选择的调度器
        /// </summary>
        public void RegisterAuto<TArgs>(
            in TriggerPlan<TArgs> plan,
            TriggerPredicate<TArgs> predicate,
            TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            _registry.RegisterToAll(in plan, predicate, executor);
        }

        /// <summary>
        /// 注册已配置的持续行为执行器
        /// 使用 ContinuousExecutorBase
        /// </summary>
        public int RegisterContinuousExecutor<TCtx>(
            ContinuousExecutorBase<TCtx> executor,
            float intervalMs = 0,
            int maxExecutions = -1)
            where TCtx : class
        {
            // 注册到静态注册器
            return ContinuousExecutorRegistry.Register(executor, intervalMs);
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update(float deltaTimeMs)
        {
            _registry.Update(deltaTimeMs, _currentContext);
            // ✅ 更新 ActionScheduler（Trigger 级别之后）
            _actionSchedulerManager.Update(deltaTimeMs, _currentContext);
        }

        /// <summary>
        /// 中断所有持续行为
        /// </summary>
        public void InterruptAllContinuous(string reason = null)
        {
            _registry.Timed.InterruptAll(0, reason ?? "User interrupt");
        }

        /// <summary>
        /// 获取活跃的持续行为数量
        /// </summary>
        public int GetActiveContinuousCount()
        {
            return _registry.Timed.RegisteredCount;
        }

        /// <summary>
        /// 获取总注册数量
        /// </summary>
        public int GetTotalRegisteredCount()
        {
            return _registry.TotalRegisteredCount;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            _registry.Dispose();
        }
    }
}