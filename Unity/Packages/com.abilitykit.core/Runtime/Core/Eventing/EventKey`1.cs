using System;

namespace AbilityKit.Core.Common.Event
{
    public readonly struct EventKey<TArgs> : IEquatable<EventKey<TArgs>>
    {
        private readonly byte _kind;
        public readonly string StringId;
        public readonly int IntId;

        public EventKey(string id)
        {
            _kind = 1;
            StringId = id;
            IntId = default;
        }

        public EventKey(int id)
        {
            _kind = 2;
            StringId = null;
            IntId = id;
        }

        public bool Equals(EventKey<TArgs> other)
        {
            if (_kind != other._kind) return false;
            if (_kind == 2) return IntId == other.IntId;
            return string.Equals(StringId, other.StringId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EventKey<TArgs> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var idHash = _kind == 2 ? IntId : (StringId != null ? StringComparer.Ordinal.GetHashCode(StringId) : 0);
                return (_kind * 397) ^ idHash;
            }
        }
    }
}
