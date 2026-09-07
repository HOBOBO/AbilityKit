using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Blackboard
{
    using AbilityKit.BehaviorTree.Definition;

    public sealed class Blackboard
    {
        private readonly BlackboardSchema _schema;
        private readonly Dictionary<string, int> _slots;
        private readonly bool[] _bools;
        private readonly long[] _int64s;
        private readonly long[] _fixedRaw;
        private readonly string?[] _strings;

        private Blackboard(BlackboardSchema schema)
        {
            _schema = CloneSchema(schema);
            _slots = new Dictionary<string, int>(_schema.Keys.Count, StringComparer.Ordinal);
            _bools = new bool[_schema.Keys.Count];
            _int64s = new long[_schema.Keys.Count];
            _fixedRaw = new long[_schema.Keys.Count];
            _strings = new string?[_schema.Keys.Count];

            for (var i = 0; i < _schema.Keys.Count; i++)
            {
                _slots.Add(_schema.Keys[i].Name, i);
            }
        }

        public BlackboardSchema Schema => CloneSchema(_schema);

        public static Blackboard Create(BlackboardSchema schema)
        {
            var blackboard = new Blackboard(schema);
            blackboard.ApplyDefaults();
            return blackboard;
        }

        public bool GetBool(string key) => _bools[SlotOf(key, ValueType.Bool)];

        public long GetInt64(string key) => _int64s[SlotOf(key, ValueType.Int64)];

        public Fixed64 GetFixed64(string key) => Fixed64.FromRaw(_fixedRaw[SlotOf(key, ValueType.Fixed64)]);

        public string GetString(string key) => _strings[SlotOf(key, ValueType.String)] ?? "";

        public bool TryGetBool(string key, out bool value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == ValueType.Bool)
            {
                value = _bools[slot];
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetInt64(string key, out long value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == ValueType.Int64)
            {
                value = _int64s[slot];
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetFixed64(string key, out Fixed64 value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == ValueType.Fixed64)
            {
                value = Fixed64.FromRaw(_fixedRaw[slot]);
                return true;
            }
            value = default;
            return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == ValueType.String)
            {
                value = _strings[slot] ?? "";
                return true;
            }
            value = "";
            return false;
        }

        public void SetBool(string key, bool value) => _bools[SlotOf(key, ValueType.Bool)] = value;

        public void SetInt64(string key, long value) => _int64s[SlotOf(key, ValueType.Int64)] = value;

        public void SetFixed64(string key, Fixed64 value) => _fixedRaw[SlotOf(key, ValueType.Fixed64)] = value.RawValue;

        public void SetString(string key, string value) => _strings[SlotOf(key, ValueType.String)] = value;

        public BlackboardValueSnapshot CaptureValues()
        {
            var snapshot = new BlackboardValueSnapshot();
            foreach (var key in _schema.Keys)
            {
                snapshot.KeyNames.Add(key.Name);
                snapshot.KeyTypes.Add(key.Type);
            }
            snapshot.BoolValues.AddRange(_bools);
            snapshot.Int64Values.AddRange(_int64s);
            snapshot.Fixed64RawValues.AddRange(_fixedRaw);
            snapshot.StringValues.AddRange(_strings!);
            return snapshot;
        }

        public void RestoreValues(BlackboardValueSnapshot snapshot)
        {
            if (snapshot == null) return;
            RestoreValuesCore(snapshot);
        }

        internal AbilityKit.BehaviorTree.BtBlackboard Inner => AbilityKit.BehaviorTree.BtBlackboard.Wrap(this);

        internal static Blackboard FromLegacy(AbilityKit.BehaviorTree.BtBlackboard inner) => inner.Canonical;

        private int SlotOf(string key, ValueType expected)
        {
            if (!_slots.TryGetValue(key, out var slot))
                throw new KeyNotFoundException($"BT blackboard key '{key}' is not declared in the tree schema.");

            var actual = _schema.Keys[slot].Type;
            if (actual != expected)
                throw new InvalidOperationException(
                    $"BT blackboard key '{key}' is declared as {actual}, accessed as {expected}.");

            return slot;
        }

        private void ApplyDefaults()
        {
            for (var i = 0; i < _schema.Keys.Count; i++)
            {
                var key = _schema.Keys[i];
                if (key.Default == null) continue;
                if (key.Default.Type != key.Type) continue;
                switch (key.Type)
                {
                    case ValueType.Bool:
                        _bools[i] = key.Default.BoolValue;
                        break;
                    case ValueType.Int64:
                        _int64s[i] = key.Default.Int64Value;
                        break;
                    case ValueType.Fixed64:
                        _fixedRaw[i] = key.Default.Fixed64Raw;
                        break;
                    case ValueType.String:
                        _strings[i] = key.Default.StringValue;
                        break;
                }
            }
        }

        private void RestoreValuesCore(BlackboardValueSnapshot snapshot)
        {
            ValidateValues(snapshot);

            for (var i = 0; i < _bools.Length; i++) _bools[i] = snapshot.BoolValues[i];
            for (var i = 0; i < _int64s.Length; i++) _int64s[i] = snapshot.Int64Values[i];
            for (var i = 0; i < _fixedRaw.Length; i++) _fixedRaw[i] = snapshot.Fixed64RawValues[i];
            for (var i = 0; i < _strings.Length; i++) _strings[i] = snapshot.StringValues[i];
        }

        internal void ValidateValues(BlackboardValueSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.KeyNames.Count != _schema.Keys.Count)
                throw new InvalidOperationException("BT blackboard snapshot key count does not match the schema.");
            if (snapshot.KeyTypes.Count != _schema.Keys.Count
                || snapshot.BoolValues.Count != _schema.Keys.Count
                || snapshot.Int64Values.Count != _schema.Keys.Count
                || snapshot.Fixed64RawValues.Count != _schema.Keys.Count
                || snapshot.StringValues.Count != _schema.Keys.Count)
            {
                throw new InvalidOperationException("BT blackboard snapshot value array count does not match the schema.");
            }

            for (var i = 0; i < snapshot.KeyNames.Count; i++)
            {
                if (!string.Equals(snapshot.KeyNames[i], _schema.Keys[i].Name, StringComparison.Ordinal)
                    || snapshot.KeyTypes[i] != _schema.Keys[i].Type)
                    throw new InvalidOperationException("BT blackboard snapshot keys do not match the schema.");
            }
        }

        private static BlackboardSchema CloneSchema(BlackboardSchema schema)
        {
            var clone = new BlackboardSchema();
            foreach (var key in schema.Keys)
            {
                clone.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default == null ? null : TreeDefinition.CloneValue(key.Default),
                });
            }
            return clone;
        }
    }
}
