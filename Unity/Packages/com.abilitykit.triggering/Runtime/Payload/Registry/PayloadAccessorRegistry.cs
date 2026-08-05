using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Payload
{
    public interface IPayloadAccessorRegistry
    {
        bool TryGetInt<TArgs>(in TArgs args, int fieldId, out int value);

        bool TryGetDouble<TArgs>(in TArgs args, int fieldId, out double value);

        bool TryGetInt(in object args, int fieldId, out int value);

        bool TryGetDouble(in object args, int fieldId, out double value);

        bool TryIsFieldSupported(Type argsType, int fieldId, out bool supported);
    }

    public sealed class PayloadAccessorRegistry : IPayloadAccessorRegistry
    {
        private readonly Dictionary<Type, object> _intAccessorsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _doubleAccessorsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, IBoxedIntAccessor> _boxedIntAccessorsByArgsType = new Dictionary<Type, IBoxedIntAccessor>();
        private readonly Dictionary<Type, IBoxedDoubleAccessor> _boxedDoubleAccessorsByArgsType = new Dictionary<Type, IBoxedDoubleAccessor>();
        private readonly Dictionary<Type, Func<int, bool>> _fieldSupportByArgsType = new Dictionary<Type, Func<int, bool>>();

        public void RegisterIntAccessor<TArgs>(IPayloadIntAccessor<TArgs> accessor, Func<int, bool> supportsField = null)
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _intAccessorsByArgsType[typeof(TArgs)] = accessor;
            _boxedIntAccessorsByArgsType[typeof(TArgs)] = new BoxedIntAccessor<TArgs>(accessor);
            RegisterFieldSupport<TArgs>(supportsField);
        }

        public void RegisterDoubleAccessor<TArgs>(IPayloadDoubleAccessor<TArgs> accessor, Func<int, bool> supportsField = null)
        {
            if (accessor == null) throw new ArgumentNullException(nameof(accessor));
            _doubleAccessorsByArgsType[typeof(TArgs)] = accessor;
            _boxedDoubleAccessorsByArgsType[typeof(TArgs)] = new BoxedDoubleAccessor<TArgs>(accessor);
            RegisterFieldSupport<TArgs>(supportsField);
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

        public bool TryGetInt(in object args, int fieldId, out int value)
        {
            if (args != null
                && _boxedIntAccessorsByArgsType.TryGetValue(args.GetType(), out var accessor))
            {
                return accessor.TryGet(args, fieldId, out value);
            }

            value = default;
            return false;
        }

        public bool TryGetDouble(in object args, int fieldId, out double value)
        {
            if (args != null
                && _boxedDoubleAccessorsByArgsType.TryGetValue(args.GetType(), out var accessor)
                && accessor.TryGet(args, fieldId, out value))
            {
                return true;
            }

            if (TryGetInt(in args, fieldId, out var intValue))
            {
                value = intValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryIsFieldSupported(Type argsType, int fieldId, out bool supported)
        {
            if (argsType != null && _fieldSupportByArgsType.TryGetValue(argsType, out var supportsField))
            {
                supported = supportsField(fieldId);
                return true;
            }

            supported = false;
            return false;
        }

        private void RegisterFieldSupport<TArgs>(Func<int, bool> supportsField)
        {
            if (supportsField != null)
            {
                _fieldSupportByArgsType[typeof(TArgs)] = supportsField;
            }
        }

        private interface IBoxedIntAccessor
        {
            bool TryGet(object args, int fieldId, out int value);
        }

        private interface IBoxedDoubleAccessor
        {
            bool TryGet(object args, int fieldId, out double value);
        }

        private sealed class BoxedIntAccessor<TArgs> : IBoxedIntAccessor
        {
            private readonly IPayloadIntAccessor<TArgs> _accessor;

            public BoxedIntAccessor(IPayloadIntAccessor<TArgs> accessor)
            {
                _accessor = accessor;
            }

            public bool TryGet(object args, int fieldId, out int value)
            {
                if (args is TArgs typed)
                {
                    return _accessor.TryGet(in typed, fieldId, out value);
                }

                value = default;
                return false;
            }
        }

        private sealed class BoxedDoubleAccessor<TArgs> : IBoxedDoubleAccessor
        {
            private readonly IPayloadDoubleAccessor<TArgs> _accessor;

            public BoxedDoubleAccessor(IPayloadDoubleAccessor<TArgs> accessor)
            {
                _accessor = accessor;
            }

            public bool TryGet(object args, int fieldId, out double value)
            {
                if (args is TArgs typed)
                {
                    return _accessor.TryGet(in typed, fieldId, out value);
                }

                value = default;
                return false;
            }
        }
    }
}
