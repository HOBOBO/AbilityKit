using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 状态包（存储 Action 的运行时状态）
    /// 每帧可能变化，需要支持快照
    /// </summary>
    [Serializable]
    public sealed class StateBag
    {
        private readonly Dictionary<string, TypedValue> _values = new();

        /// <summary>
        /// 设置状态（泛型）
        /// </summary>
        public void Set<T>(string key, T value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取状态（泛型）
        /// </summary>
        public T Get<T>(string key) where T : class => _values.TryGetValue(key, out var value) ? value.As<T>() : null;

        /// <summary>
        /// 尝试获取状态
        /// </summary>
        public bool TryGet<T>(string key, out T value) where T : class
        {
            if (_values.TryGetValue(key, out var v) && v.TypeCode != (TypeCode)TypedValue.TypeCodeValue.Empty)
            {
                value = v.As<T>();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 检查是否包含状态
        /// </summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>
        /// 移除状态
        /// </summary>
        public void Remove(string key) => _values.Remove(key);

        /// <summary>
        /// 获取所有键
        /// </summary>
        public IEnumerable<string> Keys => _values.Keys;

        /// <summary>
        /// 克隆状态包
        /// </summary>
        public StateBag Clone()
        {
            var clone = new StateBag();
            foreach (var kv in _values)
                clone._values[kv.Key] = kv.Value;
            return clone;
        }

        /// <summary>
        /// 从另一个状态包复制
        /// </summary>
        public void CopyFrom(StateBag other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Clear();
            foreach (var kv in other._values)
                _values[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 清空所有状态
        /// </summary>
        public void Clear() => _values.Clear();

        /// <summary>
        /// 获取数值状态
        /// </summary>
        public double GetDouble(string key) => _values.TryGetValue(key, out var v) ? v.AsDouble() : 0;

        /// <summary>
        /// 设置数值状态
        /// </summary>
        public void SetDouble(string key, double value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取对象状态
        /// </summary>
        public object GetObject(string key) => _values.TryGetValue(key, out var v) ? v.AsObject() : null;

        /// <summary>
        /// 设置对象状态
        /// </summary>
        public void SetObject(string key, object value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取整数状态
        /// </summary>
        public int GetInt(string key) => _values.TryGetValue(key, out var v) ? v.AsInt() : 0;

        /// <summary>
        /// 设置整数状态
        /// </summary>
        public void SetInt(string key, int value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取布尔状态
        /// </summary>
        public bool GetBool(string key) => _values.TryGetValue(key, out var v) && v.AsBool();

        /// <summary>
        /// 设置布尔状态
        /// </summary>
        public void SetBool(string key, bool value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取浮点数状态
        /// </summary>
        public float GetFloat(string key) => _values.TryGetValue(key, out var v) ? v.AsFloat() : 0f;

        /// <summary>
        /// 设置浮点数状态
        /// </summary>
        public void SetFloat(string key, float value) => _values[key] = TypedValue.From(value);
    }
}
