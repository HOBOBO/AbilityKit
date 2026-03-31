using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Triggering.Payload
{
    /// <summary>
    /// 强类型 Payload 结构体包装器
    /// 避免通过接口访问 Payload 时的装箱开销
    /// </summary>
    /// <typeparam name="T">Payload 类型（必须是结构体）</typeparam>
    public readonly struct PayloadStruct<T> where T : struct
    {
        /// <summary>
        /// 原始 Payload 值
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// 创建一个 PayloadStruct
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PayloadStruct(T value)
        {
            Value = value;
        }

        /// <summary>
        /// 将 Payload 转换为 PayloadStruct
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PayloadStruct<T>(T payload) => new PayloadStruct<T>(payload);

        /// <summary>
        /// 从值创建 PayloadStruct
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PayloadStruct<T> FromValue(T value) => new PayloadStruct<T>(value);

        /// <summary>
        /// 获取指定字段的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TField Get<TField>(PayloadField<T, TField> field)
        {
            return field.GetValue(Value);
        }

        /// <summary>
        /// 尝试获取指定字段的值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<TField>(PayloadField<T, TField> field, out TField value)
        {
            try
            {
                value = field.GetValue(Value);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    /// <summary>
    /// Payload 字段描述符
    /// 编译时定义的强类型字段访问器
    /// </summary>
    /// <typeparam name="TArgs">Payload 类型</typeparam>
    /// <typeparam name="TField">字段类型</typeparam>
    public readonly struct PayloadField<TArgs, TField> where TArgs : struct
    {
        public readonly int FieldId;
        public readonly string FieldName;
        private readonly Func<TArgs, TField> _getter;

        public PayloadField(int fieldId, string fieldName, Func<TArgs, TField> getter)
        {
            FieldId = fieldId;
            FieldName = fieldName;
            _getter = getter;
        }

        /// <summary>
        /// 通过字段名称创建（需要注册字段映射）
        /// </summary>
        public static PayloadField<TArgs, TField> Create(int fieldId, string fieldName, Func<TArgs, TField> getter)
        {
            return new PayloadField<TArgs, TField>(fieldId, fieldName, getter);
        }

        /// <summary>
        /// 直接创建（快速路径）
        /// </summary>
        public static PayloadField<TArgs, TField> CreateDirect(Func<TArgs, TField> getter)
        {
            return new PayloadField<TArgs, TField>(-1, null, getter);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TField GetValue(TArgs args)
        {
            return _getter != null ? _getter(args) : default;
        }

        /// <summary>
        /// 从 Payload 获取字段值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TField Get(PayloadStruct<TArgs> payload)
        {
            return _getter != null ? _getter(payload.Value) : default;
        }

        /// <summary>
        /// 尝试获取字段值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(PayloadStruct<TArgs> payload, out TField value)
        {
            try
            {
                value = _getter != null ? _getter(payload.Value) : default;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    /// <summary>
    /// Payload 值访问器接口（用于解析 NumericValueRef）
    /// </summary>
    /// <typeparam name="TArgs">Payload 类型</typeparam>
    public interface IPayloadAccessor<TArgs> where TArgs : struct
    {
        bool TryGetInt(in TArgs args, int fieldId, out int value);
        bool TryGetDouble(in TArgs args, int fieldId, out double value);
        bool TryGetBool(in TArgs args, int fieldId, out bool value);
    }

    /// <summary>
    /// 委托形式的 Payload 访问器（避免装箱）
    /// </summary>
    public readonly struct DelegatePayloadAccessor<TArgs> : IPayloadAccessor<TArgs> where TArgs : struct
    {
        private readonly Func<TArgs, int, int> _intGetter;
        private readonly Func<TArgs, int, double> _doubleGetter;
        private readonly Func<TArgs, int, bool> _boolGetter;

        public DelegatePayloadAccessor(
            Func<TArgs, int, int> intGetter = null,
            Func<TArgs, int, double> doubleGetter = null,
            Func<TArgs, int, bool> boolGetter = null)
        {
            _intGetter = intGetter;
            _doubleGetter = doubleGetter;
            _boolGetter = boolGetter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInt(in TArgs args, int fieldId, out int value)
        {
            if (_intGetter != null)
            {
                try
                {
                    value = _intGetter(args, fieldId);
                    return true;
                }
                catch { }
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetDouble(in TArgs args, int fieldId, out double value)
        {
            if (_doubleGetter != null)
            {
                try
                {
                    value = _doubleGetter(args, fieldId);
                    return true;
                }
                catch { }
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBool(in TArgs args, int fieldId, out bool value)
        {
            if (_boolGetter != null)
            {
                try
                {
                    value = _boolGetter(args, fieldId);
                    return true;
                }
                catch { }
            }
            value = default;
            return false;
        }
    }
}
