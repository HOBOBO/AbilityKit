using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public readonly struct NumericVarRef : IEquatable<NumericVarRef>
    {
        public NumericVarRef(string domainId, string key)
        {
            DomainId = domainId;
            Key = key;
        }

        public string DomainId { get; }
        public string Key { get; }

        public bool Equals(NumericVarRef other)
        {
            return string.Equals(DomainId, other.DomainId, StringComparison.Ordinal)
                && string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NumericVarRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var h = 17;
                h = (h * 31) + (DomainId != null ? StringComparer.Ordinal.GetHashCode(DomainId) : 0);
                h = (h * 31) + (Key != null ? StringComparer.Ordinal.GetHashCode(Key) : 0);
                return h;
            }
        }

        public static bool operator ==(NumericVarRef left, NumericVarRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NumericVarRef left, NumericVarRef right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{DomainId}:{Key}";
        }
    }
}
