using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Registry
{
    public sealed class FunctionRegistry
    {
        private readonly Dictionary<int, Entry> _functions = new Dictionary<int, Entry>();

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

        public void Register<TDelegate>(FunctionId id, TDelegate function, bool isDeterministic)
            where TDelegate : Delegate
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            _functions[id.Value] = new Entry(function, typeof(TDelegate), isDeterministic);
        }

        public bool TryGet<TDelegate>(FunctionId id, out TDelegate function, out bool isDeterministic)
            where TDelegate : Delegate
        {
            if (_functions.TryGetValue(id.Value, out var entry) && entry.DelegateType == typeof(TDelegate))
            {
                function = (TDelegate)entry.Delegate;
                isDeterministic = entry.IsDeterministic;
                return true;
            }

            function = null;
            isDeterministic = default;
            return false;
        }
    }
}
