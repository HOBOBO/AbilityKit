using System;

namespace AbilityKit.Core.Common.Event
{
    public readonly struct EventKey : IEquatable<EventKey>
    {
        private readonly byte _kind;
        public readonly string StringId;
        public readonly int IntId;
        public readonly Type ArgsType;

        public EventKey(string id, Type argsType)
        {
            _kind = 1;
            StringId = id;
            IntId = default;
            ArgsType = argsType;
        }

        public EventKey(int id, Type argsType)
        {
            _kind = 2;
            StringId = null;
            IntId = id;
            ArgsType = argsType;
        }

        public bool Equals(EventKey other)
        {
            if (_kind != other._kind) return false;
            if (ArgsType != other.ArgsType) return false;
            if (_kind == 2) return IntId == other.IntId;
            return string.Equals(StringId, other.StringId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EventKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var idHash = _kind == 2 ? IntId : (StringId != null ? StringComparer.Ordinal.GetHashCode(StringId) : 0);
                return (((_kind * 397) ^ idHash) * 397) ^ (ArgsType != null ? ArgsType.GetHashCode() : 0);
            }
        }
    }
}
