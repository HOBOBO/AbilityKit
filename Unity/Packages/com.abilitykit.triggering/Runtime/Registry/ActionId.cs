using System;

namespace AbilityKit.Triggering.Registry
{
    public readonly struct ActionId : IEquatable<ActionId>
    {
        public readonly int Value;

        public ActionId(int value)
        {
            Value = value;
        }

        public bool Equals(ActionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ActionId other && Equals(other);
        public override int GetHashCode() => Value;
    }
}
