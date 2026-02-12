using System;
using MemoryPack;

namespace AbilityKit.Ability.World.Abstractions
{
    [MemoryPackable]
    public readonly partial struct WorldId : IEquatable<WorldId>
    {
        [MemoryPackOrder(0)] public readonly string Value;

        [MemoryPackConstructor]
        public WorldId(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;

        public bool Equals(WorldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;

        public static bool operator ==(WorldId a, WorldId b) => a.Equals(b);
        public static bool operator !=(WorldId a, WorldId b) => !a.Equals(b);
    }
}
