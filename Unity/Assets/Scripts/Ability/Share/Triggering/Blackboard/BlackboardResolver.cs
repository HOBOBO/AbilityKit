using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Blackboard
{
    public sealed class BlackboardResolver : IBlackboardResolver
    {
        private readonly Dictionary<string, IBlackboard> _map;

        public BlackboardResolver()
        {
            _map = new Dictionary<string, IBlackboard>(StringComparer.Ordinal);
        }

        public bool TryResolve(string boardId, out IBlackboard blackboard)
        {
            if (boardId == null)
            {
                blackboard = null;
                return false;
            }

            return _map.TryGetValue(boardId, out blackboard);
        }

        public void Register(string boardId, IBlackboard blackboard)
        {
            if (boardId == null) throw new ArgumentNullException(nameof(boardId));
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));

            _map[boardId] = blackboard;
        }
    }
}
