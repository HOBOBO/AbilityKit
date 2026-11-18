using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留应用迁移期间使用的三层设置兼容入口，不再接收新使用方。
    /// </summary>
    /// <remarks>
    /// 此类型在下一个主版本进入移除窗口。键按序号比较，
    /// 覆盖层优先于持久层和基础层；高层空值仍会遮蔽低层值。
    /// 非线程安全；标量读取只查询所需键，完整合并仅由显式快照方法执行。
    /// </remarks>
    [Obsolete("Application settings no longer belong in Core. Move settings to the owning application package; this compatibility API will be removed in the next major version.")]
    public sealed class LayeredJsonSettingsStore
    {
        private readonly Dictionary<string, object> _base = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _persistent = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _overrides = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>
        /// 在变更提交后同步通知；单键变更传入键名，整层变更传入空引用。
        /// </summary>
        /// <remarks>
        /// 订阅者异常向调用方传播，但不回滚已提交的设置。
        /// 重复赋值及替换空层仍会通知。
        /// </remarks>
        public event Action<string?>? OnChanged;

        /// <summary>
        /// 取得基础层的实时只读接口视图；不是独立快照。
        /// </summary>
        public IReadOnlyDictionary<string, object> BaseValues => _base;

        /// <summary>
        /// 取得持久层的实时只读接口视图；不是独立快照。
        /// </summary>
        public IReadOnlyDictionary<string, object> PersistentValues => _persistent;

        /// <summary>
        /// 取得覆盖层的实时只读接口视图；不是独立快照。
        /// </summary>
        public IReadOnlyDictionary<string, object> OverrideValues => _overrides;

        /// <summary>
        /// 替换基础层并发送整层通知；空设置表示清空，空键被忽略。
        /// </summary>
        public void ReplaceBase(FlatJsonSettings? settings)
        {
            _base.Clear();
            if (settings != null)
            {
                foreach (var kv in settings.Values)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    _base[kv.Key] = kv.Value;
                }
            }
            OnChanged?.Invoke(null);
        }

        /// <summary>
        /// 替换持久层并发送整层通知；空设置表示清空，空键被忽略。
        /// </summary>
        public void ReplacePersistent(FlatJsonSettings? settings)
        {
            _persistent.Clear();
            if (settings != null)
            {
                foreach (var kv in settings.Values)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    _persistent[kv.Key] = kv.Value;
                }
            }
            OnChanged?.Invoke(null);
        }

        /// <summary>
        /// 清空非空覆盖层并发送整层通知；覆盖层已空时不通知。
        /// </summary>
        public void ClearOverrides()
        {
            if (_overrides.Count == 0) return;
            _overrides.Clear();
            OnChanged?.Invoke(null);
        }

        /// <summary>
        /// 写入覆盖层并发送单键通知；空键被忽略，即使值未改变也通知。
        /// </summary>
        public void SetOverride(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _overrides[key] = value;
            OnChanged?.Invoke(key);
        }

        /// <summary>
        /// 移除指定覆盖项；仅在键存在且被移除时发送单键通知并返回成功。
        /// </summary>
        public bool ClearOverride(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_overrides.Remove(key)) return false;
            OnChanged?.Invoke(key);
            return true;
        }

        /// <summary>
        /// 按覆盖层、持久层、基础层读取首个存在的键；高层空值仍视为存在。
        /// </summary>
        /// <remarks>
        /// 键为空或缺失时返回失败并输出空引用，不尝试转换值。
        /// </remarks>
        public bool TryGetRaw(string key, out object? value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;

            if (_overrides.TryGetValue(key, out value)) return true;
            if (_persistent.TryGetValue(key, out value)) return true;
            if (_base.TryGetValue(key, out value)) return true;
            return false;
        }

        /// <summary>
        /// 按三层优先级取得首个存在的值，再沿用扁平设置的布尔转换规则。
        /// </summary>
        /// <remarks>
        /// 高层空值或转换失败会遮蔽低层值；失败时输出默认布尔值。
        /// </remarks>
        public bool TryGetBool(string key, out bool value)
        {
            TryGetRaw(key, out var raw);
            return FlatJsonSettings.TryConvertBool(raw, out value);
        }

        /// <summary>
        /// 按三层优先级取得首个存在的值，再沿用扁平设置的整数转换规则。
        /// </summary>
        /// <remarks>
        /// 高层空值或转换失败会遮蔽低层值；失败时输出零。
        /// </remarks>
        public bool TryGetInt(string key, out int value)
        {
            TryGetRaw(key, out var raw);
            return FlatJsonSettings.TryConvertInt(raw, out value);
        }

        /// <summary>
        /// 按三层优先级取得首个存在的值，再沿用扁平设置的浮点转换规则。
        /// </summary>
        /// <remarks>
        /// 高层空值或转换失败会遮蔽低层值；失败时输出零。
        /// </remarks>
        public bool TryGetFloat(string key, out float value)
        {
            TryGetRaw(key, out var raw);
            return FlatJsonSettings.TryConvertFloat(raw, out value);
        }

        /// <summary>
        /// 按三层优先级取得首个存在的值，再沿用扁平设置的字符串转换规则。
        /// </summary>
        /// <remarks>
        /// 高层空值或转换失败会遮蔽低层值；失败时输出空串，对象转换异常继续传播。
        /// </remarks>
        public bool TryGetString(string key, out string value)
        {
            TryGetRaw(key, out var raw);
            return FlatJsonSettings.TryConvertString(raw, out value);
        }

        /// <summary>
        /// 创建按三层优先级合并的独立字典快照；值对象仍与原设置共享。
        /// </summary>
        /// <remarks>
        /// 修改快照的键值不影响存储，之后的存储变更也不改变已有快照。
        /// </remarks>
        public Dictionary<string, object> BuildEffectiveView()
        {
            var dict = new Dictionary<string, object>(_base, StringComparer.Ordinal);
            foreach (var kv in _persistent) dict[kv.Key] = kv.Value;
            foreach (var kv in _overrides) dict[kv.Key] = kv.Value;
            return dict;
        }
    }
}
