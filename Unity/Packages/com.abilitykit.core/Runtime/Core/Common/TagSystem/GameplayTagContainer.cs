using System;
using System.Collections;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public sealed class GameplayTagContainer : IEnumerable<GameplayTag>
    {
        private readonly HashSet<int> _ids = new HashSet<int>();

        public int Count => _ids.Count;

        public void Clear() => _ids.Clear();

        public bool Add(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Add(tag.Id);
        }

        public bool Remove(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Remove(tag.Id);
        }

        public bool HasTagExact(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Contains(tag.Id);
        }

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;

            foreach (var id in _ids)
            {
                if (GameplayTagManager.Instance.Matches(new GameplayTag(id), tag))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAny(in GameplayTagContainer other, bool exact = false)
        {
            if (other == null || other._ids.Count == 0) return false;

            if (exact)
            {
                foreach (var id in other._ids)
                {
                    if (_ids.Contains(id)) return true;
                }
                return false;
            }

            foreach (var id in other._ids)
            {
                if (HasTag(new GameplayTag(id))) return true;
            }

            return false;
        }

        public bool HasAll(in GameplayTagContainer other, bool exact = false)
        {
            if (other == null) return true;
            if (other._ids.Count == 0) return true;

            if (exact)
            {
                foreach (var id in other._ids)
                {
                    if (!_ids.Contains(id)) return false;
                }
                return true;
            }

            foreach (var id in other._ids)
            {
                if (!HasTag(new GameplayTag(id))) return false;
            }

            return true;
        }

        public IEnumerator<GameplayTag> GetEnumerator()
        {
            foreach (var id in _ids)
            {
                yield return new GameplayTag(id);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
