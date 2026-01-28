using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Registry
{
    public sealed class ActionRegistry
    {
        private readonly Dictionary<int, Entry> _actions = new Dictionary<int, Entry>();

        private readonly struct Entry
        {
            public readonly Delegate Delegate;
            public readonly Type DelegateType;
            public readonly bool IsDeterministic;

            public Entry(Delegate @delegate, Type delegateType, bool isDeterministic)
            {
                Delegate = @delegate;
                DelegateType = delegateType;
                IsDeterministic = isDeterministic;
            }
        }

        public void Register<TDelegate>(ActionId id, TDelegate action, bool isDeterministic)
            where TDelegate : Delegate
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions[id.Value] = new Entry(action, typeof(TDelegate), isDeterministic);
        }

        public bool TryGet<TDelegate>(ActionId id, out TDelegate action, out bool isDeterministic)
            where TDelegate : Delegate
        {
            if (_actions.TryGetValue(id.Value, out var entry) && entry.DelegateType == typeof(TDelegate))
            {
                action = (TDelegate)entry.Delegate;
                isDeterministic = entry.IsDeterministic;
                return true;
            }

            action = null;
            isDeterministic = default;
            return false;
        }
    }
}
