using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 描述一次对象池配置查询，包级配置提供者可据此返回按类型和键区分的设置。
    /// </summary>
    public readonly struct PoolConfigRequest : IEquatable<PoolConfigRequest>
    {
        /// <summary>
        /// 获取区分大小写的作用域名称；构造时空名称规范化为全局名称。
        /// </summary>
        public readonly string ScopeName;
        /// <summary>
        /// 获取对象池元素类型；正常构造时不允许为空。
        /// </summary>
        public readonly Type ElementType;
        /// <summary>
        /// 获取构造时规范化的池键。
        /// </summary>
        public readonly PoolKey Key;

        /// <summary>
        /// 创建按作用域、元素类型和池键精确匹配的请求；默认结构值不是有效的查询请求。
        /// </summary>
        public PoolConfigRequest(string scopeName, Type elementType, PoolKey key = default)
        {
            ScopeName = string.IsNullOrEmpty(scopeName) ? PoolRegistry.GlobalScopeName : scopeName;
            ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
            Key = PoolKey.Normalize(key);
        }

        /// <summary>
        /// 按序号名称、元素类型和池键判断请求是否相等，不使用持久化标识。
        /// </summary>
        public bool Equals(PoolConfigRequest other)
        {
            return string.Equals(ScopeName, other.ScopeName, StringComparison.Ordinal)
                   && ElementType == other.ElementType
                   && Key.Equals(other.Key);
        }

        /// <summary>
        /// 按序号名称、元素类型和池键判断请求是否相等，不使用持久化标识。
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is PoolConfigRequest other && Equals(other);
        }

        /// <summary>
        /// 计算进程内散列码；请求须经过构造初始化，结果不得持久化或跨进程比较。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(ScopeName);
                hash = (hash * 397) ^ ElementType.GetHashCode();
                hash = (hash * 397) ^ Key.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// 返回用于日志的作用域、类型和键描述；请求须经过构造初始化。
        /// </summary>
        public override string ToString()
        {
            return $"scope={ScopeName}, type={ElementType.FullName}, key={Key}";
        }
    }
}
