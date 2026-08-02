using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public readonly struct MobaGameStartSourceKey : IEquatable<MobaGameStartSourceKey>
    {
        public readonly string Value;

        public MobaGameStartSourceKey(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("source key cannot be null or empty", nameof(value));
            Value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(MobaGameStartSourceKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MobaGameStartSourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MobaGameStartSourceKey left, MobaGameStartSourceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MobaGameStartSourceKey left, MobaGameStartSourceKey right)
        {
            return !left.Equals(right);
        }
    }

    public interface IMobaGameStartSource
    {
        MobaGameStartSourceKey Key { get; }

        int Priority { get; }

        bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec);
    }
}
