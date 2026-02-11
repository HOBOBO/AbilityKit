using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Registry
{
    public sealed class ActionRegistry
    {
        private readonly Dictionary<Key, Entry> _actions = new Dictionary<Key, Entry>();

        private readonly struct Key : IEquatable<Key>
        {
            public readonly int Id;
            public readonly Type DelegateType;

            public Key(int id, Type delegateType)
            {
                Id = id;
                DelegateType = delegateType;
            }

            public bool Equals(Key other)
            {
                return Id == other.Id && DelegateType == other.DelegateType;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Id * 397) ^ (DelegateType != null ? DelegateType.GetHashCode() : 0);
                }
            }
        }

        private readonly struct Entry
        {
            public readonly Delegate Delegate;
            public readonly bool IsDeterministic;

            public Entry(Delegate @delegate, bool isDeterministic)
            {
                Delegate = @delegate;
                IsDeterministic = isDeterministic;
            }
        }

        public void Register<TDelegate>(ActionId id, TDelegate action, bool isDeterministic)
            where TDelegate : Delegate
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions[new Key(id.Value, typeof(TDelegate))] = new Entry(action, isDeterministic);
        }

        public bool TryGet<TDelegate>(ActionId id, out TDelegate action, out bool isDeterministic)
            where TDelegate : Delegate
        {
            if (_actions.TryGetValue(new Key(id.Value, typeof(TDelegate)), out var entry))
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
