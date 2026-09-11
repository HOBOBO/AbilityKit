using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Collections
{
    /// <summary>
    /// Immutable array-backed collection for extension-produced query results.
    /// </summary>
    public sealed class TriggerCollection : IReadOnlyTriggerCollection
    {
        private readonly TriggerCollectionValue[] _values;

        public TriggerCollection(
            TriggerCollectionValueKind valueKind,
            IReadOnlyList<TriggerCollectionValue> values,
            string elementTypeId = null)
        {
            ValueKind = valueKind;
            ElementTypeId = elementTypeId ?? string.Empty;
            if (values == null || values.Count == 0)
            {
                _values = Array.Empty<TriggerCollectionValue>();
                return;
            }

            _values = new TriggerCollectionValue[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i].Kind != valueKind)
                    throw new ArgumentException(
                        $"Collection element kind mismatch at index {i}: expected={valueKind}, actual={values[i].Kind}.",
                        nameof(values));
                _values[i] = values[i];
            }
        }

        public int Count => _values.Length;
        public TriggerCollectionValueKind ValueKind { get; }
        public string ElementTypeId { get; }

        public bool TryGetValue(int index, out TriggerCollectionValue value)
        {
            if ((uint)index >= (uint)_values.Length)
            {
                value = default;
                return false;
            }

            value = _values[index];
            return true;
        }

        public static TriggerCollection FromInt32(IReadOnlyList<int> values, string elementTypeId = null)
        {
            if (values == null || values.Count == 0)
                return new TriggerCollection(
                    TriggerCollectionValueKind.Number,
                    Array.Empty<TriggerCollectionValue>(),
                    elementTypeId);

            var converted = new TriggerCollectionValue[values.Count];
            for (var i = 0; i < values.Count; i++)
                converted[i] = TriggerCollectionValue.FromNumber(values[i]);
            return new TriggerCollection(TriggerCollectionValueKind.Number, converted, elementTypeId);
        }
    }
}
