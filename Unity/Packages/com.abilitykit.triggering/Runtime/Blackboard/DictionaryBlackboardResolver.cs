using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class DictionaryBlackboardResolver : IBlackboardResolver
    {
        private readonly Dictionary<int, IBlackboard> _map;

        public DictionaryBlackboardResolver(int capacity = 16)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _map = new Dictionary<int, IBlackboard>(capacity);
        }

        public bool TryResolve(int boardId, out IBlackboard blackboard)
        {
            return _map.TryGetValue(boardId, out blackboard);
        }

        public void Register(int boardId, IBlackboard blackboard)
        {
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));
            _map[boardId] = blackboard;
        }

        public bool Unregister(int boardId)
        {
            return _map.Remove(boardId);
        }

        public void Clear()
        {
            _map.Clear();
        }
    }
}
