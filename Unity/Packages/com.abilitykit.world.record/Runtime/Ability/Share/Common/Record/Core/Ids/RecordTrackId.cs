using System;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    [Serializable]
    public readonly struct RecordTrackId : IEquatable<RecordTrackId>
    {
        public readonly int Value;

        public RecordTrackId(int value)
        {
            Value = value;
        }

        public static RecordTrackId FromName(string name)
        {
            return new RecordTrackId(RecordIdHash.Fnv1a32(name));
        }

        public bool Equals(RecordTrackId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecordTrackId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static implicit operator RecordTrackId(string name) => FromName(name);
        public static implicit operator int(RecordTrackId id) => id.Value;

        public static bool operator ==(RecordTrackId a, RecordTrackId b) => a.Value == b.Value;
        public static bool operator !=(RecordTrackId a, RecordTrackId b) => a.Value != b.Value;
    }
}
