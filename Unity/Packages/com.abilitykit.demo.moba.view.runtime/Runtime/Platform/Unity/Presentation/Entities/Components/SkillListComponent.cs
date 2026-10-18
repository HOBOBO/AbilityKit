using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class SkillListComponent
    {
        private readonly List<SkillViewData> _items;

        public SkillListComponent(int capacity = 4)
        {
            _items = new List<SkillViewData>(Math.Max(0, capacity));
        }

        public IReadOnlyList<SkillViewData> Items => _items;

        public void Clear()
        {
            _items.Clear();
        }

        public SkillViewData Find(int skillId)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var s = _items[i];
                if (s != null && s.SkillId == skillId) return s;
            }

            return null;
        }

        public SkillViewData GetOrAdd(int skillId)
        {
            var s = Find(skillId);
            if (s != null) return s;

            s = new SkillViewData { SkillId = skillId };
            _items.Add(s);
            return s;
        }

        public bool Remove(int skillId)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var s = _items[i];
                if (s != null && s.SkillId == skillId)
                {
                    _items.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
