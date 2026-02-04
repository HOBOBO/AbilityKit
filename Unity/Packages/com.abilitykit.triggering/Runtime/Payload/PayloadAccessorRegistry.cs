using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Payload
{
    public interface IPayloadAccessorRegistry
    {
        bool TryGetInt<TArgs>(in TArgs args, int fieldId, out int value);

        bool TryGetDouble<TArgs>(in TArgs args, int fieldId, out double value);
    }

    public sealed class PayloadAccessorRegistry : IPayloadAccessorRegistry
    {
        private readonly Dictionary<Type, object> _intAccessorsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _doubleAccessorsByArgsType = new Dictionary<Type, object>();

        public void RegisterIntAccessor<TArgs>(IPayloadIntAccessor<TArgs> accessor)
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _intAccessorsByArgsType[typeof(TArgs)] = accessor;
        }

        public void RegisterDoubleAccessor<TArgs>(IPayloadDoubleAccessor<TArgs> accessor)
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _doubleAccessorsByArgsType[typeof(TArgs)] = accessor;
        }

        public bool TryGetInt<TArgs>(in TArgs args, int fieldId, out int value)
        {
            if (_intAccessorsByArgsType.TryGetValue(typeof(TArgs), out var obj) && obj is IPayloadIntAccessor<TArgs> accessor)
            {
                return accessor.TryGet(in args, fieldId, out value);
            }

            value = default;
            return false;
        }

        public bool TryGetDouble<TArgs>(in TArgs args, int fieldId, out double value)
        {
            if (_doubleAccessorsByArgsType.TryGetValue(typeof(TArgs), out var obj) && obj is IPayloadDoubleAccessor<TArgs> accessor)
            {
                return accessor.TryGet(in args, fieldId, out value);
            }

            if (TryGetInt(in args, fieldId, out var iv))
            {
                value = iv;
                return true;
            }

            value = default;
            return false;
        }
    }
}
