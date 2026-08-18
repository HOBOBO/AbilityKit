using System;
using System.Collections.Generic;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 数据流上下文默认实现
    /// 提供强类型的数据存储和访问
    /// </summary>
    public class DataflowContext : IDataflowContext
    {
        private readonly struct SlotKey : IEquatable<SlotKey>
        {
            public SlotKey(string name, Type valueType)
            {
                Name = name;
                ValueType = valueType;
            }

            private string Name { get; }
            private Type ValueType { get; }

            public bool Equals(SlotKey other)
            {
                return ValueType == other.ValueType &&
                       string.Equals(Name, other.Name, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SlotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(Name) * 397) ^ ValueType.GetHashCode();
                }
            }
        }

        /// <summary>
        /// 内部数据存储（使用槽位名称和数据类型作为键）
        /// </summary>
        private readonly Dictionary<SlotKey, object> _data = new Dictionary<SlotKey, object>();

        /// <summary>
        /// 数据流请求的源对象
        /// </summary>
        private object _source;

        /// <summary>
        /// 执行是否被中断
        /// </summary>
        private bool _isAborted;

        /// <inheritdoc />
        public object Source => _source;

        /// <inheritdoc />
        public bool IsAborted
        {
            get => _isAborted;
            set => _isAborted = value;
        }

        /// <inheritdoc />
        public void SetSource(object source)
        {
            _source = source;
        }

        /// <inheritdoc />
        public void Abort()
        {
            _isAborted = true;
        }

        /// <inheritdoc />
        public T GetData<T>(DataflowSlot<T> slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (_data.TryGetValue(GetKey(slot), out var value))
            {
                return value == null ? default : (T)value;
            }
            return slot.GetDefault();
        }

        /// <inheritdoc />
        public T GetData<T>(DataflowSlot<T> slot, T defaultValue)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (_data.TryGetValue(GetKey(slot), out var value))
            {
                return value == null ? default : (T)value;
            }
            return defaultValue;
        }

        /// <inheritdoc />
        public void SetData<T>(DataflowSlot<T> slot, T value)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }
            _data[GetKey(slot)] = value;
        }

        /// <inheritdoc />
        public bool TryGetData<T>(DataflowSlot<T> slot, out T value)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (_data.TryGetValue(GetKey(slot), out var obj))
            {
                value = obj == null ? default : (T)obj;
                return true;
            }
            value = default;
            return false;
        }

        /// <inheritdoc />
        public bool ContainsData<T>(DataflowSlot<T> slot)
        {
            if (slot == null)
            {
                return false;
            }
            return _data.ContainsKey(GetKey(slot));
        }

        /// <inheritdoc />
        public void Clear()
        {
            Reset();
        }

        /// <summary>
        /// 重置上下文状态（用于对象池回收）
        /// </summary>
        public virtual void Reset()
        {
            _data.Clear();
            _source = null;
            _isAborted = false;
        }

        private static SlotKey GetKey<T>(DataflowSlot<T> slot)
        {
            return new SlotKey(slot.Name, typeof(T));
        }
    }
}
