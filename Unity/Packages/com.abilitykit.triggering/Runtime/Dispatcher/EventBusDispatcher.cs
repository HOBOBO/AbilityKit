using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 事件总线触发器调度器
    /// 通过 EventBus 的 Publish/Subscribe 机制触发触发器
    /// </summary>
    public class EventBusDispatcher : TriggerDispatcherBase
    {
        private readonly IEventBus _eventBus;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public override EDispatcherType DispatcherType => EDispatcherType.Event;

        public EventBusDispatcher(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Name = "EventBusDispatcher";
            Priority = 0;
        }

        public override void Initialize()
        {
            _subscriptions.Clear();
            _registrations.Clear();
        }

        public override void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }
            _subscriptions.Clear();
            _registrations.Clear();
        }

        public override void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            var registration = new DispatcherRegistration<TArgs>(
                plan.TriggerId,
                in plan,
                predicate,
                executor);

            _registrations[plan.TriggerId] = registration;

            // 获取 EventKey 并订阅
            var eventKey = GetEventKey<TArgs>(plan.TriggerId);

            Action<TArgs, ExecutionControl> handler = (args, control) =>
            {
                var ctx = new EventBusDispatcherContext(null, 0);
                var dispatcherCtx = new EventBusDispatcherContextWrapper(ctx, control);

                if (predicate != null && !predicate(args, dispatcherCtx))
                {
                    return;
                }

                executor(args, dispatcherCtx);
            };

            var subscription = _eventBus.Subscribe(eventKey, handler);

            _subscriptions.Add(subscription);
        }

        public override bool Unregister(int triggerId)
        {
            // EventBus 的订阅需要在外部管理，这里只移除注册信息
            return _registrations.Remove(triggerId);
        }

        public override void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            // EventBusDispatcher 不需要每帧更新，通过订阅事件自动触发
        }

        public override int RegisteredCount => _registrations.Count;

        private static EventKey<TArgs> GetEventKey<TArgs>(int triggerId)
        {
            return new EventKey<TArgs>(StableStringId.Get($"trigger:{triggerId}"));
        }
    }

    /// <summary>
    /// 事件总线上下文包装器
    /// </summary>
    public class EventBusDispatcherContextWrapper : ITriggerDispatcherContext
    {
        private readonly EventBusDispatcherContext _innerContext;
        private readonly ExecutionControl _control;

        public EventBusDispatcherContextWrapper(EventBusDispatcherContext innerContext, ExecutionControl control)
        {
            _innerContext = innerContext;
            _control = control;
        }

        public object Context => _innerContext.Context;
        public float CurrentTimeMs => _innerContext.CurrentTimeMs;
        public ExecutionControl Control => _control;

        public T GetService<T>() where T : class
        {
            return _innerContext.GetService<T>();
        }
    }
}
