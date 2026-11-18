using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 在元素类型及作用域内标识对象池，字符串按序号区分大小写；默认结构值应先规范化再作为散列键。
    /// </summary>
    public readonly struct PoolKey : IEquatable<PoolKey>
    {
        /// <summary>
        /// 获取池键原始字段；构造函数规范化空引用，默认结构值仍可能保存空引用，应先调用 Normalize。
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// 获取使用空字符串表示的默认池键。
        /// </summary>
        public static readonly PoolKey Default = new PoolKey(string.Empty);

        /// <summary>
        /// 创建池键，将空引用规范化为空字符串；字符串按序号比较，不作空白裁剪。
        /// </summary>
        /// <param name="value">按序号区分大小写的键值，空引用转为空字符串。</param>
        public PoolKey(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// 将空字符串键或默认结构值规范化为 Default，其它键保持原值。
        /// </summary>
        /// <param name="key">元素类型及作用域内的池键，管理器入口会规范化默认结构值。</param>
        /// <returns>规范化后的池键。</returns>
        public static PoolKey Normalize(PoolKey key)
        {
            return string.IsNullOrEmpty(key.Value) ? Default : key;
        }

        /// <inheritdoc/>
        /// <summary>
        /// 按序号比较原始字段，区分大小写；直接比较默认结构值前应先规范化。
        /// </summary>
        public bool Equals(PoolKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        /// <summary>
        /// 按序号比较原始字段，区分大小写；直接比较默认结构值前应先规范化。
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is PoolKey other && Equals(other);
        }

        /// <inheritdoc/>
        /// <summary>
        /// 计算进程内的区分大小写散列码；默认结构值须先规范化，结果不得持久化或跨端使用。
        /// </summary>
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc/>
        /// <summary>
        /// 返回原始键字段，不额外执行规范化。
        /// </summary>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// 从区分大小写的字符串创建池键，空引用规范化为空字符串。
        /// </summary>
        /// <param name="value">按序号区分大小写的键值，空引用转为空字符串。</param>
        public static implicit operator PoolKey(string value) => new PoolKey(value);
    }
}
