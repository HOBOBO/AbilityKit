using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 描述一次对象池配置查询，包级配置提供者可据此返回按类型和键区分的设置。
    /// </summary>
    public readonly struct PoolConfigRequest : IEquatable<PoolConfigRequest>
    {
        public readonly string ScopeName;
        public readonly Type ElementType;
        public readonly PoolKey Key;

        public PoolConfigRequest(string scopeName, Type elementType, PoolKey key = default)
        {
            ScopeName = string.IsNullOrEmpty(scopeName) ? PoolRegistry.GlobalScopeName : scopeName;
            ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
            Key = PoolKey.Normalize(key);
        }

        public bool Equals(PoolConfigRequest other)
        {
            return string.Equals(ScopeName, other.ScopeName, StringComparison.Ordinal)
                   && ElementType == other.ElementType
                   && Key.Equals(other.Key);
        }

        public override bool Equals(object? obj)
        {
            return obj is PoolConfigRequest other && Equals(other);
        }

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

        public override string ToString()
        {
            return $"scope={ScopeName}, type={ElementType.FullName}, key={Key}";
        }
    }
}
