using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 通用管理器，用于管理键值对的映射关系
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public class GenericIdManager<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _keyToValueMap = new Dictionary<TKey, TValue>();
        private readonly Dictionary<TValue, TKey> _valueToKeyMap = new Dictionary<TValue, TKey>();
        private TValue _nextId = default;
        private readonly Func<TValue, TValue> _incrementFunc;

        public GenericIdManager(Func<TValue, TValue> incrementFunc)
        {
            _incrementFunc = incrementFunc ?? Increment;
        }
        public TValue Register(TKey key)
        {
            if (_keyToValueMap.TryGetValue(key, out var existingValue))
            {
                return existingValue;
            }

            TValue newValue = _nextId;
            _keyToValueMap[key] = newValue;
            _valueToKeyMap[newValue] = key;
            _nextId = _incrementFunc(_nextId); // 自增ID
            return newValue;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _keyToValueMap.TryGetValue(key, out value);
        }

        public bool TryGetKey(TValue value, out TKey key)
        {
            return _valueToKeyMap.TryGetValue(value, out key);
        }
        
        private TValue Increment(TValue value)
        {
            // 根据类型实现自增逻辑
            object boxed = value;

            if (boxed is int i) return (TValue)(object)(i + 1);
            if (boxed is long l) return (TValue)(object)(l + 1L);
            if (boxed is short s) return (TValue)(object)(short)(s + 1);
            if (boxed is byte b) return (TValue)(object)(byte)(b + 1);
            if (boxed is uint ui) return (TValue)(object)(ui + 1U);
            if (boxed is ulong ul) return (TValue)(object)(ul + 1UL);
            if (boxed is ushort us) return (TValue)(object)(ushort)(us + 1);

            if (boxed is Enum)
            {
                var enumType = typeof(TValue);
                var underlying = Enum.GetUnderlyingType(enumType);

                if (underlying == typeof(int)) return (TValue)Enum.ToObject(enumType, Convert.ToInt32(boxed) + 1);
                if (underlying == typeof(long)) return (TValue)Enum.ToObject(enumType, Convert.ToInt64(boxed) + 1L);
                if (underlying == typeof(short)) return (TValue)Enum.ToObject(enumType, (short)(Convert.ToInt16(boxed) + 1));
                if (underlying == typeof(byte)) return (TValue)Enum.ToObject(enumType, (byte)(Convert.ToByte(boxed) + 1));
                if (underlying == typeof(uint)) return (TValue)Enum.ToObject(enumType, Convert.ToUInt32(boxed) + 1U);
                if (underlying == typeof(ulong)) return (TValue)Enum.ToObject(enumType, Convert.ToUInt64(boxed) + 1UL);
                if (underlying == typeof(ushort)) return (TValue)Enum.ToObject(enumType, (ushort)(Convert.ToUInt16(boxed) + 1));
            }

            throw new InvalidOperationException($"Type '{typeof(TValue)}' does not support default increment. Please provide incrementFunc in GenericIdManager constructor.");
        }
    }
}