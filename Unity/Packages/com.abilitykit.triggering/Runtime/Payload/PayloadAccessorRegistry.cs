using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Payload
{
    public interface IPayloadAccessorRegistry
    {
        bool TryGetInt<TArgs>(in TArgs args, int fieldId, out int value);
    }

    public sealed class PayloadAccessorRegistry : IPayloadAccessorRegistry
    {
        private readonly Dictionary<Type, object> _intAccessorsByArgsType = new Dictionary<Type, object>();

        public void RegisterIntAccessor<TArgs>(IPayloadIntAccessor<TArgs> accessor)
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _intAccessorsByArgsType[typeof(TArgs)] = accessor;
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
    }
}
