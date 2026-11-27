using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    /// <remarks>
    /// 订阅部分按参数类型和事件键管理事件槽；每个槽拥有自己的注册列表和总线句柄。
    /// </remarks>
    public sealed partial class TriggerRunner<TCtx>
    {
        /*
         * 字典的键与其中字典的泛型参数对应；只在相同的 TArgs 下还原类型。
         */
        private readonly Dictionary<Type, object> _eventSlotsByArgsType = new Dictionary<Type, object>();
        private long _registrationOrder;

        private EventSlot<TArgs> GetOrCreateEventSlot<TArgs>(EventKey<TArgs> key)
        {
            var type = typeof(TArgs);
            if (!_eventSlotsByArgsType.TryGetValue(type, out var slotsForType))
            {
                slotsForType = new Dictionary<EventKey<TArgs>, EventSlot<TArgs>>();
                _eventSlotsByArgsType.Add(type, slotsForType);
            }

            var slots = (Dictionary<EventKey<TArgs>, EventSlot<TArgs>>)slotsForType;
            if (!slots.TryGetValue(key, out var slot))
            {
                slot = new EventSlot<TArgs>(this);
                slots.Add(key, slot);
            }

            return slot;
        }

        /// <summary>
        /// 管理一个类型化事件的注册和总线订阅。
        /// 退订后复用槽，使总线已捕获但尚未进入运行器的回调仍能读取该事件当前的注册列表。
        /// </summary>
        private sealed class EventSlot<TArgs>
        {
            private readonly TriggerRunner<TCtx> _runner;
            private readonly TriggerRunnerEntryList<TArgs, TCtx> _entries = new TriggerRunnerEntryList<TArgs, TCtx>();
            private IDisposable _subscription;

            public EventSlot(TriggerRunner<TCtx> runner)
            {
                _runner = runner;
            }

            /// <summary>
            /// 确保总线已订阅，再插入条目并发出注册通知。
            /// </summary>
            public IDisposable Register(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority)
            {
                _subscription ??= Subscribe(key);

                var entry = new TriggerRunnerEntry<TArgs, TCtx>(phase, priority, _runner._registrationOrder++, trigger);
                _entries.InsertSorted(entry);
                var registration = new TriggerRunnerRegistration<TArgs>(this, key, entry);
                _runner.NotifyRegistered(key, entry);
                return registration;
            }

            /// <summary>
            /// 先移除条目，再以注销通知返回时的列表状态决定是否退订。
            /// </summary>
            public void Unregister(EventKey<TArgs> key, TriggerRunnerEntry<TArgs, TCtx> entry)
            {
                _entries.Remove(entry);
                _runner.NotifyUnregistered(key, entry);
                if (_entries.Count != 0) return;

                var subscription = _subscription;
                _subscription = null;
                subscription?.Dispose();
            }

            private IDisposable Subscribe(EventKey<TArgs> key) =>
                _runner._eventBus.Subscribe(key, (args, control) => _runner.Dispatch(key, in args, control, _entries));
        }
    }
}
