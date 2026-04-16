using System;
using System.Globalization;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 类型安全的数值包装
    /// 支持常见类型：double, int, float, bool, string, 以及引用类型
    /// </summary>
    [Serializable]
    public readonly struct TypedValue : IEquatable<TypedValue>
    {
        private readonly object _value;
        private readonly TypeCode _typeCode;

        internal enum TypeCodeValue
        {
            Empty = 0,
            Object = 1,
            Double = 2,
            Int32 = 3,
            Int64 = 4,
            Single = 5,
            Boolean = 6,
            String = 7,
            Vector3 = 8
        }

        private TypeCodeValue TypeCodeValueInternal => _typeCode switch
        {
            TypeCode.Empty => TypeCodeValue.Empty,
            TypeCode.Object => TypeCodeValue.Object,
            TypeCode.Double => TypeCodeValue.Double,
            TypeCode.Int32 => TypeCodeValue.Int32,
            TypeCode.Int64 => TypeCodeValue.Int64,
            TypeCode.Single => TypeCodeValue.Single,
            TypeCode.Boolean => TypeCodeValue.Boolean,
            TypeCode.String => TypeCodeValue.String,
            _ => TypeCodeValue.Object
        };

        private TypedValue(object value, TypeCode typeCode)
        {
            _value = value;
            _typeCode = typeCode;
        }

        /// <summary>
        /// 从值创建 TypedValue（自动推断类型）
        /// </summary>
        public static TypedValue From<T>(T value)
        {
            if (value == null)
                return default;

            var type = typeof(T);

            // 值类型
            if (type.IsEnum)
                return new TypedValue(value, TypeCode.Int32);

            return type switch
            {
                _ when type == typeof(double) => new TypedValue(value, TypeCode.Double),
                _ when type == typeof(float) => new TypedValue((double)(float)(object)value, TypeCode.Double),
                _ when type == typeof(int) => new TypedValue(value, TypeCode.Int32),
                _ when type == typeof(long) => new TypedValue(value, TypeCode.Int64),
                _ when type == typeof(bool) => new TypedValue(value, TypeCode.Boolean),
                _ when type == typeof(string) => new TypedValue(value, TypeCode.String),
                _ when typeof(Abstractions.Vector3).IsAssignableFrom(type) => new TypedValue(value, TypeCode.Object),
                _ => new TypedValue(value, TypeCode.Object)
            };
        }

        /// <summary>
        /// 转换为指定类型
        /// </summary>
        public T As<T>()
        {
            if (_value is T t) return t;
            if (_value == null) return default;

            var targetType = typeof(T);

            // 处理装箱值类型
            try
            {
                if (targetType.IsEnum && _value is int intValue)
                    return (T)Enum.ToObject(targetType, intValue);

                if (targetType == typeof(double))
                {
                    return (T)Convert.ChangeType(ConvertToDouble(_value, _typeCode), typeof(double));
                }

                if (targetType == typeof(float))
                {
                    return (T)(object)(float)ConvertToDouble(_value, _typeCode);
                }

                if (targetType == typeof(int))
                {
                    return (T)Convert.ChangeType(ConvertToInt(_value, _typeCode), typeof(int));
                }

                if (targetType == typeof(bool))
                {
                    if (_value is bool b) return (T)(object)b;
                    if (_value is int i) return (T)(object)(i != 0);
                    if (_value is double d) return (T)(object)(d != 0);
                }

                if (targetType == typeof(string))
                {
                    return (T)(object)_value.ToString();
                }

                if (targetType == typeof(Abstractions.Vector3))
                {
                    if (_value is Abstractions.Vector3 v) return (T)(object)v;
                }

                // 引用类型直接转换
                if (!targetType.IsValueType && _value is T result)
                    return result;
            }
            catch
            {
                // 转换失败返回默认值
            }

            return default;
        }

        /// <summary>
        /// 作为对象获取原始值
        /// </summary>
        public object AsObject() => _value;

        /// <summary>
        /// 作为 double 获取
        /// </summary>
        public double AsDouble() => _value is double d ? d : ConvertToDouble(_value, _typeCode);

        /// <summary>
        /// 作为 int 获取
        /// </summary>
        public int AsInt() => _value is int i ? i : Convert.ToInt32(AsDouble());

        /// <summary>
        /// 作为 float 获取
        /// </summary>
        public float AsFloat() => _value is float f ? f : (float)AsDouble();

        /// <summary>
        /// 作为 bool 获取
        /// </summary>
        public bool AsBool()
        {
            if (_value is bool b) return b;
            if (_value is int i) return i != 0;
            if (_value is double d) return d != 0;
            if (_value is float f) return f != 0f;
            return false;
        }

        /// <summary>
        /// 作为字符串获取
        /// </summary>
        public string AsString() => _value?.ToString();

        /// <summary>
        /// 获取原始值
        /// </summary>
        public object RawValue => _value;

        /// <summary>
        /// 获取类型代码
        /// </summary>
        public TypeCode TypeCode => _typeCode;

        /// <summary>
        /// 判断是否为空
        /// </summary>
        public bool IsEmpty => _value == null;

        // ========== 类型转换辅助方法 ==========

        private static double ConvertToDouble(object value, TypeCode typeCode)
        {
            return typeCode switch
            {
                TypeCode.Double => (double)value,
                TypeCode.Single => (float)value,
                TypeCode.Int32 => (int)value,
                TypeCode.Int64 => (long)value,
                TypeCode.UInt32 => (uint)value,
                TypeCode.UInt64 => (ulong)value,
                TypeCode.Byte => (byte)value,
                TypeCode.SByte => (sbyte)value,
                TypeCode.Int16 => (short)value,
                TypeCode.UInt16 => (ushort)value,
                TypeCode.Decimal => Convert.ToDouble(value),
                TypeCode.Boolean => (bool)value ? 1.0 : 0.0,
                _ => value is IConvertible ? Convert.ToDouble(value) : 0.0
            };
        }

        private static int ConvertToInt(object value, TypeCode typeCode)
        {
            return typeCode switch
            {
                TypeCode.Int32 => (int)value,
                TypeCode.Int64 => (int)(long)value,
                TypeCode.Double => (int)(double)value,
                TypeCode.Single => (int)(float)value,
                TypeCode.Boolean => (bool)value ? 1 : 0,
                _ => Convert.ToInt32(value)
            };
        }

        // ========== 运算符重载 ==========

        public static implicit operator TypedValue(double value) => From(value);
        public static implicit operator TypedValue(int value) => From(value);
        public static implicit operator TypedValue(float value) => From(value);
        public static implicit operator TypedValue(bool value) => From(value);
        public static implicit operator TypedValue(string value) => From(value);
        public static implicit operator TypedValue(Abstractions.Vector3 value) => From(value);

        // ========== 相等性比较 ==========

        public bool Equals(TypedValue other)
        {
            if (_typeCode != other._typeCode) return false;
            if (_value == null && other._value == null) return true;
            if (_value == null || other._value == null) return false;

            return _value.Equals(other._value);
        }

        public override bool Equals(object obj) => obj is TypedValue other && Equals(other);

        public override int GetHashCode() => _value?.GetHashCode() ?? 0;

        public static bool operator ==(TypedValue left, TypedValue right) => left.Equals(right);
        public static bool operator !=(TypedValue left, TypedValue right) => !left.Equals(right);

        public override string ToString()
        {
            if (_value == null) return "null";
            return _value.ToString();
        }
    }
}
