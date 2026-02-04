using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class DictionaryBlackboard : IBlackboard
    {
        private readonly Dictionary<int, int> _ints;

        private readonly Dictionary<int, bool> _bools;
        private readonly Dictionary<int, float> _floats;
        private readonly Dictionary<int, double> _doubles;
        private readonly Dictionary<int, string> _strings;

        public DictionaryBlackboard(int capacity = 16)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ints = new Dictionary<int, int>(capacity);
            _bools = new Dictionary<int, bool>(capacity);
            _floats = new Dictionary<int, float>(capacity);
            _doubles = new Dictionary<int, double>(capacity);
            _strings = new Dictionary<int, string>(capacity);
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
    }
}
