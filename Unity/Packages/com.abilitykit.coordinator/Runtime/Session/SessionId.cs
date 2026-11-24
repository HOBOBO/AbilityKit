using System;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public readonly struct SessionId : IEquatable<SessionId>
    {
        public long Value { get; }

        public SessionId(long value)
        {
            Value = value;
        }

        public static SessionId New()
        {
            return new SessionId(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static SessionId None => new SessionId(0);

        public bool Equals(SessionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SessionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(SessionId left, SessionId right) => left.Equals(right);
        public static bool operator !=(SessionId left, SessionId right) => !left.Equals(right);
    }
}
