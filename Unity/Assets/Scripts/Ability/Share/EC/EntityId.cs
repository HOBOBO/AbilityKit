using System;

namespace AbilityKit.Ability.EC
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly int Index;
        public readonly int Version;

        public EntityId(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool Equals(EntityId other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Version;
            }
        }

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

        public override string ToString()
        {
            return $"EntityId({Index},{Version})";
        }
    }
}
