using System;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 与具体实体后端无关的稳定目标句柄。
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0UL;

        public EntityId(ulong value)
        {
            Value = value;
        }

        public EntityId(int value)
        {
            Value = value > 0 ? (ulong)value : 0UL;
        }

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString();

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
        public static implicit operator EntityId(int value) => new EntityId(value);
        public static implicit operator EntityId(ulong value) => new EntityId(value);

        public static readonly EntityId Invalid = default;
    }
}
