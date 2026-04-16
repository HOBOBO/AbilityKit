using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 参数包（存储 Action 的配置参数）
    /// 在 Action 创建时注入，执行期间不应修改
    /// </summary>
    [Serializable]
    public sealed class ParameterBag
    {
        private readonly Dictionary<string, TypedValue> _values = new();

        /// <summary>
        /// 设置参数（泛型）
        /// </summary>
        public void Set<T>(string key, T value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取参数（泛型）
        /// </summary>
        public T Get<T>(string key) => _values.TryGetValue(key, out var value) ? value.As<T>() : default;

        /// <summary>
        /// 检查是否包含参数
        /// </summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>
        /// 移除参数
        /// </summary>
        public void Remove(string key) => _values.Remove(key);

        /// <summary>
        /// 获取所有键
        /// </summary>
        public IEnumerable<string> Keys => _values.Keys;

        /// <summary>
        /// 获取所有值
        /// </summary>
        public IEnumerable<TypedValue> Values => _values.Values;

        /// <summary>
        /// 克隆参数包
        /// </summary>
        public ParameterBag Clone()
        {
            var clone = new ParameterBag();
            foreach (var kv in _values)
                clone._values[kv.Key] = kv.Value;
            return clone;
        }

        /// <summary>
        /// 从另一个参数包复制
        /// </summary>
        public void CopyFrom(ParameterBag other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Clear();
            foreach (var kv in other._values)
                _values[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 清空所有参数
        /// </summary>
        public void Clear() => _values.Clear();

        // ========== 类型专用方法（性能优化）==========

        /// <summary>
        /// 设置数值参数（double 专用，避免装箱）
        /// </summary>
        public void SetDouble(string key, double value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取数值参数（double 专用）
        /// </summary>
        public double GetDouble(string key) => _values.TryGetValue(key, out var v) ? v.AsDouble() : 0;

        /// <summary>
        /// 设置对象参数
        /// </summary>
        public void SetObject(string key, object value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取对象参数
        /// </summary>
        public object GetObject(string key) => _values.TryGetValue(key, out var v) ? v.AsObject() : null;

        /// <summary>
        /// 获取对象参数（泛型）
        /// </summary>
        public T GetObject<T>(string key) where T : class => GetObject(key) as T;

        /// <summary>
        /// 设置字符串参数
        /// </summary>
        public void SetString(string key, string value) => _values[key] = TypedValue.From(value);

        /// <summary>
        /// 获取字符串参数
        /// </summary>
        public string GetString(string key) => _values.TryGetValue(key, out var v) ? v.AsString() : null;

        /// <summary>
        /// 检查是否包含数值参数
        /// </summary>
        public bool HasNumeric(string key) => _values.ContainsKey(key);

        /// <summary>
        /// 获取枚举参数
        /// </summary>
        public T GetEnum<T>(string key) where T : struct => _values.TryGetValue(key, out var v) ? v.As<T>() : default;

        /// <summary>
        /// 设置枚举参数
        /// </summary>
        public void SetEnum<T>(string key, T value) where T : struct => _values[key] = TypedValue.From(value);
    }
}
