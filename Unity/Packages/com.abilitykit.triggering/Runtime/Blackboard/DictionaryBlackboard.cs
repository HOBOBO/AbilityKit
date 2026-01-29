using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class DictionaryBlackboard : IBlackboard
    {
        private readonly Dictionary<int, int> _ints;

        public DictionaryBlackboard(int capacity = 16)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ints = new Dictionary<int, int>(capacity);
        }

        public bool TryGetInt(int keyId, out int value)
        {
            return _ints.TryGetValue(keyId, out value);
        }

        public void SetInt(int keyId, int value)
        {
            _ints[keyId] = value;
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
        }
    }
}
