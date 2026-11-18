using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留应用迁移期间使用的扁平设置兼容入口，不再接收新使用方。
    /// </summary>
    /// <remarks>
    /// 此类型在下一个主版本进入移除窗口。键按序号比较，值对象不会被深复制。
    /// 非线程安全；字符串解析和格式化沿用当前区域设置，不构成确定性数值契约。
    /// </remarks>
    [Obsolete("Application settings no longer belong in Core. Move settings to the owning application package; this compatibility API will be removed in the next major version.")]
    public sealed class FlatJsonSettings
    {
        private readonly Dictionary<string, object> _values;

        /// <summary>
        /// 复制输入字典；空引用按空设置处理，值对象仍由调用方和设置共同引用。
        /// </summary>
        public FlatJsonSettings(Dictionary<string, object>? values)
        {
            _values = values != null
                ? new Dictionary<string, object>(values, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 取得内部键值的只读接口视图；不会复制字典或值对象。
        /// </summary>
        public IReadOnlyDictionary<string, object> Values => _values;

        /// <summary>
        /// 尝试将指定值转换为布尔值；支持布尔、整数、浮点及布尔或整数字符串。
        /// </summary>
        /// <remarks>
        /// 键为空、缺失、值为空或类型不支持时返回失败并输出默认值。
        /// 浮点判断保留单精度十的负六次方、双精度十的负十二次方的原有容差。
        /// </remarks>
        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;
            return TryConvertBool(raw, out value);
        }

        /// <summary>
        /// 复用兼容布尔转换规则，使分层读取无需先复制整份设置。
        /// </summary>
        internal static bool TryConvertBool(object? raw, out bool value)
        {
            value = default;
            if (raw == null) return false;

            if (raw is bool b)
            {
                value = b;
                return true;
            }

            if (raw is int i)
            {
                value = i != 0;
                return true;
            }

            if (raw is long l)
            {
                value = l != 0;
                return true;
            }

            if (raw is float f)
            {
                value = global::System.Math.Abs(f) > 1e-6f;
                return true;
            }

            if (raw is double d)
            {
                value = global::System.Math.Abs(d) > 1e-12;
                return true;
            }

            if (raw is string s)
            {
                if (bool.TryParse(s, out var sb))
                {
                    value = sb;
                    return true;
                }

                if (long.TryParse(s, out var sl))
                {
                    value = sl != 0;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试将指定值转换为整数；失败时返回假并输出零。
        /// </summary>
        /// <remarks>
        /// 支持整数、浮点、布尔及整数字符串。长整数越界时失败；
        /// 浮点值沿用运行时显式转整数行为，不额外校验范围或非有限值。
        /// </remarks>
        public bool TryGetInt(string key, out int value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;
            return TryConvertInt(raw, out value);
        }

        /// <summary>
        /// 复用兼容整数转换规则，保持长整数范围检查及浮点显式转换行为。
        /// </summary>
        internal static bool TryConvertInt(object? raw, out int value)
        {
            value = default;
            if (raw == null) return false;

            if (raw is int i)
            {
                value = i;
                return true;
            }

            if (raw is long l)
            {
                if (l is >= int.MinValue and <= int.MaxValue)
                {
                    value = (int)l;
                    return true;
                }
                return false;
            }

            if (raw is float f)
            {
                value = (int)f;
                return true;
            }

            if (raw is double d)
            {
                value = (int)d;
                return true;
            }

            if (raw is bool b)
            {
                value = b ? 1 : 0;
                return true;
            }

            if (raw is string s && int.TryParse(s, out var si))
            {
                value = si;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试将整数、单精度、双精度或浮点字符串转换为单精度值；失败输出零。
        /// </summary>
        /// <remarks>
        /// 双精度转单精度保留原有精度损失和非有限值行为；布尔值不参与转换。
        /// </remarks>
        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;
            return TryConvertFloat(raw, out value);
        }

        /// <summary>
        /// 复用兼容浮点转换规则，保留原有精度损失及当前区域设置的解析行为。
        /// </summary>
        internal static bool TryConvertFloat(object? raw, out float value)
        {
            value = default;
            if (raw == null) return false;

            if (raw is float f)
            {
                value = f;
                return true;
            }

            if (raw is double d)
            {
                value = (float)d;
                return true;
            }

            if (raw is int i)
            {
                value = i;
                return true;
            }

            if (raw is long l)
            {
                value = l;
                return true;
            }

            if (raw is string s && float.TryParse(s, out var sf))
            {
                value = sf;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 字符串原样返回，其他非空值调用自身的字符串转换方法；失败输出空串。
        /// </summary>
        /// <remarks>
        /// 原值为空串时成功；其他对象转换为空串或空引用时失败。
        /// 对象自身转换时抛出的异常向调用方传播。
        /// </remarks>
        public bool TryGetString(string key, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;
            return TryConvertString(raw, out value);
        }

        /// <summary>
        /// 复用兼容字符串转换规则；对象自身的字符串转换异常继续向调用方传播。
        /// </summary>
        internal static bool TryConvertString(object? raw, out string value)
        {
            value = string.Empty;
            if (raw == null) return false;

            if (raw is string s)
            {
                value = s;
                return true;
            }

            var text = raw.ToString();
            if (string.IsNullOrEmpty(text)) return false;
            value = text;
            return true;
        }

        /// <summary>
        /// 创建不含任何键值的独立设置实例。
        /// </summary>
        public static FlatJsonSettings Empty() => new FlatJsonSettings(null);

        /// <summary>
        /// 使用调用方提供的反序列化方法创建设置；缺少输入或发生异常时返回空设置。
        /// </summary>
        /// <remarks>
        /// 此兼容入口隔离反序列化和字典复制异常，不记录日志，也不向调用方传播。
        /// </remarks>
        public static FlatJsonSettings FromJson(string json, Func<string, Dictionary<string, object>> deserialize)
        {
            if (deserialize == null) return Empty();
            if (string.IsNullOrEmpty(json)) return Empty();
            try
            {
                var dict = deserialize(json);
                return new FlatJsonSettings(dict);
            }
            catch
            {
                return Empty();
            }
        }
    }
}
