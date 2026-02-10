using System;

namespace AbilityKit.Effects.Core
{
    public readonly struct EffectScopeKey : IEquatable<EffectScopeKey>
    {
        public readonly byte KindId;
        public readonly int Id;

        public EffectScopeKey(byte kindId, int id)
        {
            KindId = kindId;
            Id = id;
        }

        public bool Equals(EffectScopeKey other) => KindId == other.KindId && Id == other.Id;
        public override bool Equals(object obj) => obj is EffectScopeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(KindId, Id);
        public override string ToString() => $"{KindId}:{Id}";
    }
}
