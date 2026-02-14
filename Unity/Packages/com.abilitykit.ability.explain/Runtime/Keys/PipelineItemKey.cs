using System;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public readonly struct PipelineItemKey : IEquatable<PipelineItemKey>
    {
        public readonly string Type;
        public readonly string Id;
        public readonly string Variant;
        public readonly string Domain;

        public PipelineItemKey(string type, string id, string variant = null, string domain = null)
        {
            Type = type ?? string.Empty;
            Id = id ?? string.Empty;
            Variant = variant;
            Domain = domain;
        }

        public bool Equals(PipelineItemKey other)
        {
            return string.Equals(Type, other.Type, StringComparison.Ordinal)
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(Variant, other.Variant, StringComparison.Ordinal)
                && string.Equals(Domain, other.Domain, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PipelineItemKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (Type != null ? Type.GetHashCode() : 0);
                hash = hash * 31 + (Id != null ? Id.GetHashCode() : 0);
                hash = hash * 31 + (Variant != null ? Variant.GetHashCode() : 0);
                hash = hash * 31 + (Domain != null ? Domain.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Domain) && !string.IsNullOrEmpty(Variant)) return $"{Domain}:{Type}:{Id}@{Variant}";
            if (!string.IsNullOrEmpty(Domain)) return $"{Domain}:{Type}:{Id}";
            if (!string.IsNullOrEmpty(Variant)) return $"{Type}:{Id}@{Variant}";
            return $"{Type}:{Id}";
        }
    }
}
