using System;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public readonly struct AttributeId : IEquatable<AttributeId>
    {
        internal readonly int Id;

        internal AttributeId(int id)
        {
            Id = id;
        }

        public static AttributeId FromRaw(int id)
        {
            return new AttributeId(id);
        }

        public bool IsValid => Id != 0;

        public string Name => AttributeRegistry.Instance.GetName(this);

        public bool Equals(AttributeId other) => Id == other.Id;

        public override bool Equals(object obj) => obj is AttributeId other && Equals(other);

        public override int GetHashCode() => Id;

        public static bool operator ==(AttributeId a, AttributeId b) => a.Id == b.Id;

        public static bool operator !=(AttributeId a, AttributeId b) => a.Id != b.Id;

        public override string ToString() => Name ?? string.Empty;
    }
}
