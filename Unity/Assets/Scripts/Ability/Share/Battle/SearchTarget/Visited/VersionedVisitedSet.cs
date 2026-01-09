using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Visited
{
    public sealed class VersionedVisitedSet : IVisitedSet
    {
        private readonly Dictionary<int, int> _marks;
        private int _version;

        public VersionedVisitedSet(IEqualityComparer<int> comparer = null)
        {
            _marks = new Dictionary<int, int>(comparer);
            _version = 1;
        }

        public void Next()
        {
            _version++;
            if (_version == int.MaxValue)
            {
                _marks.Clear();
                _version = 1;
            }
        }

        public bool Mark(int actorId)
        {
            if (actorId <= 0) return false;

            if (_marks.TryGetValue(actorId, out var v) && v == _version)
            {
                return false;
            }

            _marks[actorId] = _version;
            return true;
        }

        public bool IsMarked(int actorId)
        {
            return actorId > 0 && _marks.TryGetValue(actorId, out var v) && v == _version;
        }

        public void Clear()
        {
            _marks.Clear();
            _version = 1;
        }
    }
}
