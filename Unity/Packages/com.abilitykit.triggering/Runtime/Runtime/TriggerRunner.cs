using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class TriggerRunner
    {
        private readonly IEventBus _eventBus;
        private readonly ITriggerContextSource _contextSource;
        private readonly ITriggerObserver _observer;

        private readonly FunctionRegistry _functions;
        private readonly ActionRegistry _actions;
        private readonly IBlackboardResolver _blackboards;
        private readonly IPayloadAccessorRegistry _payloads;
        private readonly IIdNameRegistry _idNames;
        private readonly ILegacyTriggerExecutor _legacy;
        private readonly ExecPolicy _policy;

        private readonly Dictionary<Type, object> _triggerListsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _subscriptionsByArgsType = new Dictionary<Type, object>();
        private long _registrationOrder;

        public TriggerRunner(IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, ITriggerContextSource contextSource = null, ITriggerObserver observer = null, IBlackboardResolver blackboards = null, IPayloadAccessorRegistry payloads = null, IIdNameRegistry idNames = null, ILegacyTriggerExecutor legacy = null, ExecPolicy policy = default)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _functions = functions ?? throw new ArgumentNullException(nameof(functions));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _contextSource = contextSource;
            _observer = observer;
            _blackboards = blackboards;
            _payloads = payloads;
            _idNames = idNames;
            _legacy = legacy;
            _policy = policy;
        }

        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs> trigger, int phase = 0, int priority = 0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            var list = GetOrCreateTriggerList<TArgs>();
            if (!list.TryGetValue(key, out var triggers))
            {
                triggers = new List<Entry<TArgs>>(4);
                list.Add(key, triggers);
                EnsureSubscribed(key, list);
            }

            var entry = new Entry<TArgs>(phase, priority, _registrationOrder++, trigger);
            InsertSorted(triggers, entry);
            return new Registration<TArgs>(triggers, entry);
        }

        private Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> GetOrCreateTriggerList<TArgs>()
        {
            var type = typeof(TArgs);
            if (_triggerListsByArgsType.TryGetValue(type, out var obj)) return (Dictionary<EventKey<TArgs>, List<Entry<TArgs>>>)obj;

            var dict = new Dictionary<EventKey<TArgs>, List<Entry<TArgs>>>();
            _triggerListsByArgsType.Add(type, dict);
            return dict;
        }

        private void EnsureSubscribed<TArgs>(EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
        {
            var type = typeof(TArgs);
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.ContainsKey(key)) return;
                var dispatcher = new Dispatcher<TArgs>(this, key, list);
                subs[key] = _eventBus.Subscribe(key, dispatcher.OnEvent);
                return;
            }

            var newSubs = new Dictionary<EventKey<TArgs>, IDisposable>();
            _subscriptionsByArgsType.Add(type, newSubs);
            {
                var dispatcher = new Dispatcher<TArgs>(this, key, list);
                newSubs[key] = _eventBus.Subscribe(key, dispatcher.OnEvent);
            }
        }

        private void Dispatch<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
        {
            if (!list.TryGetValue(key, out var triggers) || triggers.Count == 0) return;

            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            if (control == null) control = new ExecutionControl();
            control.Reset();
            var execCtx = new ExecCtx(ctx, _eventBus, _functions, _actions, _blackboards, _payloads, _idNames, _legacy, _policy, control);

            for (int i = 0; i < triggers.Count; i++)
            {
                var entry = triggers[i];
                var trigger = entry.Trigger;
                var ok = trigger.Evaluate(in args, in execCtx);
                _observer?.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok, in execCtx);

                if (control.StopPropagation || control.Cancel)
                {
                    _observer?.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, control.StopPropagation ? ETriggerShortCircuitReason.StopPropagation : ETriggerShortCircuitReason.Cancel, in execCtx);
                    break;
                }

                if (ok)
                {
                    trigger.Execute(in args, in execCtx);
                    _observer?.OnExecute(key, in args, entry.Phase, entry.Priority, entry.Order, in execCtx);

                    if (control.StopPropagation || control.Cancel)
                    {
                        _observer?.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, control.StopPropagation ? ETriggerShortCircuitReason.StopPropagation : ETriggerShortCircuitReason.Cancel, in execCtx);
                        break;
                    }
                }
            }
        }

        private static void InsertSorted<TArgs>(List<Entry<TArgs>> list, Entry<TArgs> entry)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var other = list[i];
                if (entry.Phase < other.Phase)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority < other.Priority)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority == other.Priority && entry.Order < other.Order)
                {
                    list.Insert(i, entry);
                    return;
                }
            }

            list.Add(entry);
        }

        private readonly struct Entry<TArgs>
        {
            public readonly int Phase;
            public readonly int Priority;
            public readonly long Order;
            public readonly ITrigger<TArgs> Trigger;

            public Entry(int phase, int priority, long order, ITrigger<TArgs> trigger)
            {
                Phase = phase;
                Priority = priority;
                Order = order;
                Trigger = trigger;
            }
        }

        private sealed class Dispatcher<TArgs>
        {
            private readonly TriggerRunner _runner;
            private readonly EventKey<TArgs> _key;
            private readonly Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> _list;

            public Dispatcher(TriggerRunner runner, EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
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

        private sealed class Registration<TArgs> : IDisposable
        {
            private List<Entry<TArgs>> _list;
            private Entry<TArgs> _entry;

            public Registration(List<Entry<TArgs>> list, Entry<TArgs> entry)
            {
                _list = list;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_list == null) return;
                for (int i = 0; i < _list.Count; i++)
                {
                    if (!ReferenceEquals(_list[i].Trigger, _entry.Trigger)) continue;
                    if (_list[i].Phase != _entry.Phase) continue;
                    if (_list[i].Priority != _entry.Priority) continue;
                    if (_list[i].Order != _entry.Order) continue;
                    _list.RemoveAt(i);
                    break;
                }
                _list = null;
                _entry = default;
            }
        }
    }
}
