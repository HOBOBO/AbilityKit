using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    /// <remarks>
    /// 订阅部分按事件参数类型及事件键定位注册列表与总线句柄。
    /// </remarks>
    public sealed partial class TriggerRunner<TCtx>
    {
        /*
         * 字典的键与其中字典的泛型参数对应；只在相同的 TArgs 下还原类型。
         */
        private readonly Dictionary<Type, object> _triggerListsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _subscriptionsByArgsType = new Dictionary<Type, object>();
        private long _registrationOrder;

        private Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> GetOrCreateTriggerList<TArgs>()
        {
            var type = typeof(TArgs);
            if (_triggerListsByArgsType.TryGetValue(type, out var obj)) return (Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>>)obj;

            var dict = new Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>>();
            _triggerListsByArgsType.Add(type, dict);
            return dict;
        }

        private void EnsureSubscribed<TArgs>(EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
        {
            var type = typeof(TArgs);
            var dispatcher = new Dispatcher<TArgs>(this, key, list);

            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.ContainsKey(key)) return;

                subs[key] = _eventBus.Subscribe(key, (args, control) => dispatcher.OnEvent(args, control));
                return;
            }

            var newSubs = new Dictionary<EventKey<TArgs>, IDisposable>();
            _subscriptionsByArgsType.Add(type, newSubs);

            newSubs[key] = _eventBus.Subscribe(key, (args, control) => dispatcher.OnEvent(args, control));
        }

        private IDisposable GetSubscription<TArgs>(EventKey<TArgs> key)
        {
            var type = typeof(TArgs);
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.TryGetValue(key, out var subscription))
                    return subscription;
            }
            return null;
        }

        private void TryUnsubscribe<TArgs>(EventKey<TArgs> key, IDisposable subscription)
        {
            var type = typeof(TArgs);
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                subs.Remove(key);
                subscription?.Dispose();
            }
        }

        /// <summary>
        /// 把总线回调绑定到特定事件键及其所属注册列表。
        /// </summary>
        private sealed class Dispatcher<TArgs>
        {
            private readonly TriggerRunner<TCtx> _runner;
            private readonly EventKey<TArgs> _key;
            private readonly Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> _list;

            public Dispatcher(TriggerRunner<TCtx> runner, EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
            {
                _runner = runner;
                _key = key;
                _list = list;
            }

            public void OnEvent(TArgs args, ExecutionControl control)
            {
                _runner.Dispatch(_key, in args, control, _list);
            }
        }
    }
}
