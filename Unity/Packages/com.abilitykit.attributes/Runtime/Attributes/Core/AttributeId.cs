using System;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性 ID。
    /// 表示属性的唯一标识符，持有名称以避免每次查询 Registry。
    /// 
    /// 设计说明：
    /// - 持有名称以减少对 Registry 的查询依赖
    /// - 支持类型安全的比较和哈希
    /// - Name 属性直接返回持有的名称，不查询全局注册表
    /// </summary>
    public readonly struct AttributeId : IEquatable<AttributeId>, IComparable<AttributeId>
    {
        /// <summary>内部 ID</summary>
        internal readonly int Id;

        /// <summary>属性名称（持有以避免查询 Registry）</summary>
        internal readonly string _name;

        internal AttributeId(int id, string name = null)
        {
            Id = id;
            _name = name;
        }

        /// <summary>
        /// 从原始 ID 创建 AttributeId
        /// </summary>
        public static AttributeId FromRaw(int id)
        {
            return new AttributeId(id, null);
        }

        /// <summary>
        /// 创建带有名称的 AttributeId
        /// </summary>
        public static AttributeId Create(string name, int id)
        {
            return new AttributeId(id, name);
        }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Id != 0;

        /// <summary>
        /// 属性名称。
        /// 直接返回持有的名称，避免每次查询 Registry。
        /// 如果名称为空但 ID 有效，尝试从默认注册表获取。
        /// </summary>
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_name)) return _name;
                if (Id == 0) return string.Empty;
                return AttributeRegistry.DefaultRegistry.GetName(this);
            }
        }

        /// <summary>
        /// 获取属性组名
        /// </summary>
        public string Group => IsValid ? AttributeRegistry.DefaultRegistry.GetGroup(this) : string.Empty;

        public bool Equals(AttributeId other) => Id == other.Id;

        public override bool Equals(object obj) => obj is AttributeId other && Equals(other);

        public override int GetHashCode() => Id;

        public int CompareTo(AttributeId other) => Id.CompareTo(other.Id);

        public static bool operator ==(AttributeId a, AttributeId b) => a.Id == b.Id;

        public static bool operator !=(AttributeId a, AttributeId b) => a.Id != b.Id;

        public override string ToString() => Name ?? string.Empty;
    }
}
