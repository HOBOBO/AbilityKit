using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>黑板值快照：按 schema key 顺序对齐的类型化数组。</summary>
    public sealed class BtBlackboardValueSnapshot
    {
        public List<string> KeyNames { get; set; } = new();
        public List<BtValueType> KeyTypes { get; set; } = new();
        public List<bool> BoolValues { get; set; } = new();
        public List<long> Int64Values { get; set; } = new();
        public List<long> Fixed64RawValues { get; set; } = new();
        public List<string> StringValues { get; set; } = new();
    }

    /// <summary>
    /// 类型化黑板：key 必须先在 schema 中声明；类型不匹配的读写是编程错误，直接抛出。
    /// 值存储按 schema 槽位展开（无装箱），快照为纯数组拷贝。
    /// </summary>
    public sealed class BtBlackboard
    {
        private readonly BtBlackboardSchema _schema;
        private readonly Dictionary<string, int> _slots;
        private readonly bool[] _bools;
        private readonly long[] _int64s;
        private readonly long[] _fixedRaw;
        private readonly string?[] _strings;

        public BtBlackboardSchema Schema => _schema;

        private BtBlackboard(BtBlackboardSchema schema)
        {
            _schema = schema;
            _slots = new Dictionary<string, int>(schema.Keys.Count, StringComparer.Ordinal);
            _bools = new bool[schema.Keys.Count];
            _int64s = new long[schema.Keys.Count];
            _fixedRaw = new long[schema.Keys.Count];
            _strings = new string?[schema.Keys.Count];

            for (var i = 0; i < schema.Keys.Count; i++)
            {
                _slots.Add(schema.Keys[i].Name, i);
            }
        }

        public static BtBlackboard Create(BtBlackboardSchema schema)
        {
            var blackboard = new BtBlackboard(schema);
            for (var i = 0; i < schema.Keys.Count; i++)
            {
                var key = schema.Keys[i];
                if (key.Default == null) continue;
                if (key.Default.Type != key.Type) continue;
                switch (key.Type)
                {
                    case BtValueType.Bool: blackboard._bools[i] = key.Default.BoolValue; break;
                    case BtValueType.Int64: blackboard._int64s[i] = key.Default.Int64Value; break;
                    case BtValueType.Fixed64: blackboard._fixedRaw[i] = key.Default.Fixed64Raw; break;
                    case BtValueType.String: blackboard._strings[i] = key.Default.StringValue; break;
                }
            }
            return blackboard;
        }

        private int SlotOf(string key, BtValueType expected)
        {
            if (!_slots.TryGetValue(key, out var slot))
                throw new KeyNotFoundException($"BT blackboard key '{key}' is not declared in the tree schema.");
            var actual = _schema.Keys[slot].Type;
            if (actual != expected)
                throw new InvalidOperationException(
                    $"BT blackboard key '{key}' is declared as {actual}, accessed as {expected}.");
            return slot;
        }

        public bool GetBool(string key) => _bools[SlotOf(key, BtValueType.Bool)];
        public long GetInt64(string key) => _int64s[SlotOf(key, BtValueType.Int64)];
        public Fixed64 GetFixed64(string key) => Fixed64.FromRaw(_fixedRaw[SlotOf(key, BtValueType.Fixed64)]);
        public string GetString(string key) => _strings[SlotOf(key, BtValueType.String)] ?? "";

        public bool TryGetBool(string key, out bool value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == BtValueType.Bool)
            { value = _bools[slot]; return true; }
            value = default; return false;
        }

        public bool TryGetInt64(string key, out long value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == BtValueType.Int64)
            { value = _int64s[slot]; return true; }
            value = default; return false;
        }

        public bool TryGetFixed64(string key, out Fixed64 value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == BtValueType.Fixed64)
            { value = Fixed64.FromRaw(_fixedRaw[slot]); return true; }
            value = default; return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (_slots.TryGetValue(key, out var slot) && _schema.Keys[slot].Type == BtValueType.String)
            { value = _strings[slot] ?? ""; return true; }
            value = ""; return false;
        }

        public void SetBool(string key, bool value) => _bools[SlotOf(key, BtValueType.Bool)] = value;
        public void SetInt64(string key, long value) => _int64s[SlotOf(key, BtValueType.Int64)] = value;
        public void SetFixed64(string key, Fixed64 value) => _fixedRaw[SlotOf(key, BtValueType.Fixed64)] = value.RawValue;
        public void SetString(string key, string value) => _strings[SlotOf(key, BtValueType.String)] = value;

        public BtBlackboardValueSnapshot CaptureValues()
        {
            var snapshot = new BtBlackboardValueSnapshot();
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

        public void RestoreValues(BtBlackboardValueSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (snapshot.KeyNames.Count != _schema.Keys.Count)
                throw new InvalidOperationException("BT blackboard snapshot key count does not match the schema.");

            for (var i = 0; i < snapshot.KeyNames.Count; i++)
            {
                if (!string.Equals(snapshot.KeyNames[i], _schema.Keys[i].Name, StringComparison.Ordinal)
                    || snapshot.KeyTypes[i] != _schema.Keys[i].Type)
                    throw new InvalidOperationException("BT blackboard snapshot keys do not match the schema.");
            }

            for (var i = 0; i < _bools.Length; i++) _bools[i] = snapshot.BoolValues[i];
            for (var i = 0; i < _int64s.Length; i++) _int64s[i] = snapshot.Int64Values[i];
            for (var i = 0; i < _fixedRaw.Length; i++) _fixedRaw[i] = snapshot.Fixed64RawValues[i];
            for (var i = 0; i < _strings.Length; i++) _strings[i] = snapshot.StringValues[i];
        }
    }
}
