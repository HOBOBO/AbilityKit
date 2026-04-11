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
        /// <summary>
        /// 内部数据存储（使用槽位名称作为键）
        /// </summary>
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

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

            if (_data.TryGetValue(slot.Name, out var value) && value is T typedValue)
            {
                return typedValue;
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

            if (_data.TryGetValue(slot.Name, out var value) && value is T typedValue)
            {
                return typedValue;
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
            _data[slot.Name] = value;
        }

        /// <inheritdoc />
        public bool TryGetData<T>(DataflowSlot<T> slot, out T value)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (_data.TryGetValue(slot.Name, out var obj) && obj is T typedValue)
            {
                value = typedValue;
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
            return _data.ContainsKey(slot.Name);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _data.Clear();
            _source = null;
            _isAborted = false;
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
    }
}
