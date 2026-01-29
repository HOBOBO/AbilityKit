using System;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    [Serializable]
    public readonly struct RecordEventType : IEquatable<RecordEventType>
    {
        public readonly int Value;

        public RecordEventType(int value)
        {
            Value = value;
        }

        public static RecordEventType FromName(string name)
        {
            return new RecordEventType(RecordIdHash.Fnv1a32(name));
        }

        public bool Equals(RecordEventType other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecordEventType other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static implicit operator RecordEventType(string name) => FromName(name);
        public static implicit operator int(RecordEventType t) => t.Value;

        public static bool operator ==(RecordEventType a, RecordEventType b) => a.Value == b.Value;
        public static bool operator !=(RecordEventType a, RecordEventType b) => a.Value != b.Value;
    }
}
