using System;

namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// Identifies an event channel by either a string or integer identifier and its argument type.
    /// </summary>
    public readonly struct EventKey : IEquatable<EventKey>
    {
        private readonly byte _kind;

        /// <summary>Gets the string identifier when this key was created from a string.</summary>
        public readonly string StringId;

        /// <summary>Gets the integer identifier when this key was created from an integer.</summary>
        public readonly int IntId;

        /// <summary>Gets the event argument type that separates otherwise identical identifiers.</summary>
        public readonly Type ArgsType;

        /// <summary>Creates a key from a string identifier and argument type.</summary>
        /// <param name="id">The non-null event identifier.</param>
        /// <param name="argsType">The non-null event argument type.</param>
        public EventKey(string id, Type argsType)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));

            _kind = 1;
            StringId = id;
            IntId = default;
            ArgsType = argsType;
        }

        /// <summary>Creates a key from an integer identifier and argument type.</summary>
        /// <param name="id">The event identifier.</param>
        /// <param name="argsType">The non-null event argument type.</param>
        public EventKey(int id, Type argsType)
        {
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));

            _kind = 2;
            StringId = string.Empty;
            IntId = id;
            ArgsType = argsType;
        }

        /// <inheritdoc />
        public bool Equals(EventKey other)
        {
            if (_kind != other._kind) return false;
            if (ArgsType != other.ArgsType) return false;
            if (_kind == 2) return IntId == other.IntId;
            return string.Equals(StringId, other.StringId, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is EventKey other && Equals(other);
        }

        /// <inheritdoc />
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
