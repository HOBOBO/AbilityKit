using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 实体唯一标识符。
    /// 使用 Index + Version 组合确保销毁后复用的实体不会与旧引用混淆。
    /// </summary>
    public readonly struct IEntityId : IEquatable<IEntityId>, IComparable<IEntityId>
    {
        /// <summary>数组下标。</summary>
        public readonly int Index;

        /// <summary>版本号，每次销毁后递增。</summary>
        public readonly int Version;

        public IEntityId(int index, int version)
        {
            Index = index;
            Version = version;
        }

        /// <summary>是否有效（Index >= 0）。</summary>
        public bool IsValid => Index >= 0;

        /// <summary>无效的实体ID。</summary>
        public static IEntityId Invalid => default;

        /// <summary>判断是否指向同一个实体。</summary>
        public bool Equals(IEntityId other) => Index == other.Index && Version == other.Version;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is IEntityId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => (Index * 397) ^ Version;

        /// <inheritdoc/>
        public int CompareTo(IEntityId other)
        {
            var indexCmp = Index.CompareTo(other.Index);
            return indexCmp != 0 ? indexCmp : Version.CompareTo(other.Version);
        }

        /// <summary>相等运算符。</summary>
        public static bool operator ==(IEntityId left, IEntityId right) => left.Equals(right);

        /// <summary>不相等运算符。</summary>
        public static bool operator !=(IEntityId left, IEntityId right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString() => $"({Index},v{Version})";

        /// <summary>生成调试友好的字符串。</summary>
        public string ToDebugString()
        {
            if (!IsValid) return "Invalid";
            return $"Entity[{Index}]_v{Version}";
        }
    }
}
