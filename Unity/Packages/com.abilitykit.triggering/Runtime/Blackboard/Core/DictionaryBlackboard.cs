using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class DictionaryBlackboard : IBlackboard, IBlackboardSchema, IBlackboardSnapshotParticipant
    {
        private readonly Dictionary<int, BlackboardKeySchema> _schema;
        private readonly Dictionary<int, int> _ints;

        private readonly Dictionary<int, bool> _bools;
        private readonly Dictionary<int, float> _floats;
        private readonly Dictionary<int, double> _doubles;
        private readonly Dictionary<int, string> _strings;

        public DictionaryBlackboard(int capacity = 16)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _schema = new Dictionary<int, BlackboardKeySchema>(capacity);
            _ints = new Dictionary<int, int>(capacity);
            _bools = new Dictionary<int, bool>(capacity);
            _floats = new Dictionary<int, float>(capacity);
            _doubles = new Dictionary<int, double>(capacity);
            _strings = new Dictionary<int, string>(capacity);
        }

        public void DefineKey(int keyId, BlackboardKeyType type, bool canRead = true, bool canWrite = true)
        {
            if (keyId == 0) throw new ArgumentOutOfRangeException(nameof(keyId));
            if (type == BlackboardKeyType.Unknown) throw new ArgumentOutOfRangeException(nameof(type));
            _schema[keyId] = new BlackboardKeySchema(type, canRead, canWrite);
        }

        public bool TryGetKeySchema(int keyId, out BlackboardKeySchema schema)
        {
            return _schema.TryGetValue(keyId, out schema);
        }

        public bool TryGetInt(int keyId, out int value)
        {
            return _ints.TryGetValue(keyId, out value);
        }

        public void SetInt(int keyId, int value)
        {
            _ints[keyId] = value;
        }

        public bool TryGetBool(int keyId, out bool value)
        {
            return _bools.TryGetValue(keyId, out value);
        }

        public void SetBool(int keyId, bool value)
        {
            _bools[keyId] = value;
        }

        public bool TryGetFloat(int keyId, out float value)
        {
            return _floats.TryGetValue(keyId, out value);
        }

        public void SetFloat(int keyId, float value)
        {
            _floats[keyId] = value;
        }

        public bool TryGetDouble(int keyId, out double value)
        {
            if (_doubles.TryGetValue(keyId, out value)) return true;

            if (_floats.TryGetValue(keyId, out var f))
            {
                value = f;
                return true;
            }

            if (_ints.TryGetValue(keyId, out var i))
            {
                value = i;
                return true;
            }

            if (_bools.TryGetValue(keyId, out var b))
            {
                value = b ? 1d : 0d;
                return true;
            }

            value = 0d;
            return false;
        }

        public void SetDouble(int keyId, double value)
        {
            _doubles[keyId] = value;
        }

        public bool TryGetString(int keyId, out string value)
        {
            return _strings.TryGetValue(keyId, out value);
        }

        public void SetString(int keyId, string value)
        {
            _strings[keyId] = value;
        }

        public void CopyIntsTo(List<KeyValuePair<int, int>> list)
        {
            if (list == null) return;
            list.Clear();
            foreach (var kv in _ints)
            {
                list.Add(kv);
            }
        }

        public void Clear()
        {
            _ints.Clear();
            _bools.Clear();
            _floats.Clear();
            _doubles.Clear();
            _strings.Clear();
        }

        public bool TryCaptureSnapshot(int boardId, out BlackboardSnapshotBoard snapshot, out string error)
        {
            snapshot = new BlackboardSnapshotBoard { BoardId = boardId };
            foreach (var schemaPair in _schema)
            {
                var entry = BlackboardSnapshotEntry.Missing(schemaPair.Key, schemaPair.Value.Type);
                switch (schemaPair.Value.Type)
                {
                    case BlackboardKeyType.Int:
                        if (_ints.TryGetValue(schemaPair.Key, out var intValue))
                        {
                            entry.HasValue = true;
                            entry.ValueKind = BlackboardSnapshotValueKind.Int;
                            entry.IntValue = intValue;
                        }
                        break;
                    case BlackboardKeyType.Bool:
                        if (_bools.TryGetValue(schemaPair.Key, out var boolValue))
                        {
                            entry.HasValue = true;
                            entry.ValueKind = BlackboardSnapshotValueKind.Bool;
                            entry.BoolValue = boolValue;
                        }
                        break;
                    case BlackboardKeyType.Float:
                        if (_floats.TryGetValue(schemaPair.Key, out var floatValue))
                        {
                            entry.HasValue = true;
                            entry.ValueKind = BlackboardSnapshotValueKind.Float;
                            entry.FloatValue = floatValue;
                        }
                        break;
                    case BlackboardKeyType.Double:
                        if (_doubles.TryGetValue(schemaPair.Key, out var doubleValue))
                        {
                            entry.HasValue = true;
                            entry.ValueKind = BlackboardSnapshotValueKind.Double;
                            entry.DoubleValue = doubleValue;
                        }
                        break;
                    case BlackboardKeyType.String:
                        if (_strings.TryGetValue(schemaPair.Key, out var stringValue))
                        {
                            entry.HasValue = true;
                            entry.ValueKind = BlackboardSnapshotValueKind.String;
                            entry.StringValue = stringValue;
                        }
                        break;
                    default:
                        error = $"Blackboard key type {schemaPair.Value.Type} cannot be snapshotted. keyId={schemaPair.Key}.";
                        snapshot = null;
                        return false;
                }
                snapshot.Entries.Add(entry);
            }
            snapshot.Entries.Sort((left, right) => left.KeyId.CompareTo(right.KeyId));
            error = null;
            return true;
        }

        public bool ValidateSnapshot(BlackboardSnapshotBoard snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Blackboard snapshot board is required.";
                return false;
            }
            var seen = new HashSet<int>();
            var entries = snapshot.Entries ?? new List<BlackboardSnapshotEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!seen.Add(entry.KeyId))
                {
                    error = $"Blackboard snapshot contains duplicate keyId={entry.KeyId}.";
                    return false;
                }
                if (!_schema.TryGetValue(entry.KeyId, out var schema))
                {
                    error = $"Blackboard snapshot references unknown keyId={entry.KeyId}.";
                    return false;
                }
                if (schema.Type != entry.Type)
                {
                    error = $"Blackboard snapshot type mismatch. keyId={entry.KeyId} schema={schema.Type} snapshot={entry.Type}.";
                    return false;
                }
                if (!entry.HasValue) continue;
                var expectedKind = ToSnapshotValueKind(schema.Type);
                if (entry.ValueKind != expectedKind)
                {
                    error = $"Blackboard snapshot value kind mismatch. keyId={entry.KeyId} expected={expectedKind} actual={entry.ValueKind}.";
                    return false;
                }
            }
            foreach (var schemaPair in _schema)
            {
                if (!seen.Contains(schemaPair.Key))
                {
                    error = $"Blackboard snapshot is missing keyId={schemaPair.Key}.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        public bool TryRestoreSnapshot(BlackboardSnapshotBoard snapshot, out string error)
        {
            if (!ValidateSnapshot(snapshot, out error)) return false;
            Clear();
            var entries = snapshot.Entries ?? new List<BlackboardSnapshotEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.HasValue) continue;
                switch (entry.ValueKind)
                {
                    case BlackboardSnapshotValueKind.Int: _ints[entry.KeyId] = entry.IntValue; break;
                    case BlackboardSnapshotValueKind.Bool: _bools[entry.KeyId] = entry.BoolValue; break;
                    case BlackboardSnapshotValueKind.Float: _floats[entry.KeyId] = entry.FloatValue; break;
                    case BlackboardSnapshotValueKind.Double: _doubles[entry.KeyId] = entry.DoubleValue; break;
                    case BlackboardSnapshotValueKind.String: _strings[entry.KeyId] = entry.StringValue; break;
                    default:
                        error = $"Blackboard snapshot value kind {entry.ValueKind} is not supported.";
                        return false;
                }
            }
            error = null;
            return true;
        }

        private static BlackboardSnapshotValueKind ToSnapshotValueKind(BlackboardKeyType type)
        {
            switch (type)
            {
                case BlackboardKeyType.Int: return BlackboardSnapshotValueKind.Int;
                case BlackboardKeyType.Bool: return BlackboardSnapshotValueKind.Bool;
                case BlackboardKeyType.Float: return BlackboardSnapshotValueKind.Float;
                case BlackboardKeyType.Double: return BlackboardSnapshotValueKind.Double;
                case BlackboardKeyType.String: return BlackboardSnapshotValueKind.String;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Blackboard type.");
            }
        }
    }
}
