using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class BuffListComponent
    {
        private readonly List<BuffViewData> _items;

        public BuffListComponent(int capacity = 8)
        {
            _items = new List<BuffViewData>(Math.Max(0, capacity));
        }

        public IReadOnlyList<BuffViewData> Items => _items;

        public void Clear()
        {
            _items.Clear();
        }

        public BuffViewData Find(int buffId)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var b = _items[i];
                if (b != null && b.BuffId == buffId) return b;
            }

            return null;
        }

        public BuffViewData GetOrAdd(int buffId)
        {
            var b = Find(buffId);
            if (b != null) return b;

            b = new BuffViewData { BuffId = buffId };
            _items.Add(b);
            return b;
        }

        public bool Remove(int buffId)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var b = _items[i];
                if (b != null && b.BuffId == buffId)
                {
                    _items.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
