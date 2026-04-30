namespace AbilityKit.Ability.StateSync
{
    public readonly struct StateHash : System.IEquatable<StateHash>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public StateHash(ulong value)
        {
            Value = value;
        }

        public static readonly StateHash Invalid = new StateHash(0);

        public bool Equals(StateHash other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StateHash other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16");

        public static bool operator ==(StateHash left, StateHash right) => left.Value == right.Value;
        public static bool operator !=(StateHash left, StateHash right) => left.Value != right.Value;
    }
}
