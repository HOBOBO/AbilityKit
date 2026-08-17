using System;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Tracks one published reference without taking disposal policy away from its creator.
    /// </summary>
    internal sealed class ReferenceBindingOwner<T>
        where T : class
    {
        private long _generation;
        private T _value;
        private bool _ownsValue;

        internal long Generation => _generation;
        internal T Value => _value;
        internal bool OwnsValue => _ownsValue;

        internal long Bind(T value, bool ownsValue = false)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            _generation++;
            _value = value;
            _ownsValue = ownsValue;
            return _generation;
        }

        internal bool IsCurrent(long generation, T value)
        {
            return generation == _generation && ReferenceEquals(_value, value);
        }

        internal bool TryClear(long generation, T value, out T released, out bool owned)
        {
            if (!IsCurrent(generation, value))
            {
                released = null;
                owned = false;
                return false;
            }

            Clear(out released, out owned);
            return true;
        }

        internal bool TryClear(T value, out T released, out bool owned)
        {
            if (!ReferenceEquals(_value, value))
            {
                released = null;
                owned = false;
                return false;
            }

            Clear(out released, out owned);
            return true;
        }

        internal bool Reset(out T released, out bool owned)
        {
            if (_value == null)
            {
                released = null;
                owned = false;
                return false;
            }

            Clear(out released, out owned);
            return true;
        }

        private void Clear(out T released, out bool owned)
        {
            released = _value;
            owned = _ownsValue;
            _value = null;
            _ownsValue = false;
            _generation++;
        }
    }
}
