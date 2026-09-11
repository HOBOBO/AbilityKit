using System;

namespace AbilityKit.Triggering.Collections
{
    public enum TriggerCollectionValueKind : byte
    {
        Number = 0,
        Boolean = 1,
        String = 2
    }

    /// <summary>
    /// Blackboard-compatible collection element. ElementTypeId carries optional
    /// domain meaning such as "entity" without coupling Triggering to a game package.
    /// </summary>
    public readonly struct TriggerCollectionValue : IEquatable<TriggerCollectionValue>
    {
        private TriggerCollectionValue(
            TriggerCollectionValueKind kind,
            double number,
            bool boolean,
            string text)
        {
            Kind = kind;
            Number = number;
            Boolean = boolean;
            String = text;
        }

        public TriggerCollectionValueKind Kind { get; }
        public double Number { get; }
        public bool Boolean { get; }
        public string String { get; }

        public static TriggerCollectionValue FromNumber(double value) =>
            new TriggerCollectionValue(TriggerCollectionValueKind.Number, value, false, null);

        public static TriggerCollectionValue FromBoolean(bool value) =>
            new TriggerCollectionValue(TriggerCollectionValueKind.Boolean, 0d, value, null);

        public static TriggerCollectionValue FromString(string value) =>
            new TriggerCollectionValue(TriggerCollectionValueKind.String, 0d, false, value ?? string.Empty);

        public bool Equals(TriggerCollectionValue other) =>
            Kind == other.Kind && Number.Equals(other.Number) &&
            Boolean == other.Boolean && string.Equals(String, other.String, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is TriggerCollectionValue other && Equals(other);
        public override int GetHashCode() => unchecked(
            ((((int)Kind * 397) ^ Number.GetHashCode()) * 397 ^ Boolean.GetHashCode()) * 397 ^
            (String != null ? String.GetHashCode() : 0));
    }
}
