using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class DictionaryBlackboardDomainResolver : IBlackboardDomainResolver
    {
        private readonly Dictionary<string, int> _map;

        public DictionaryBlackboardDomainResolver(int capacity = 16)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _map = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
        }

        public bool TryResolveBoardId<TCtx>(in ExecCtx<TCtx> ctx, string domainId, out int boardId)
        {
            if (domainId == null)
            {
                boardId = 0;
                return false;
            }

            return _map.TryGetValue(domainId, out boardId) && boardId != 0;
        }

        public void Register(string domainId, int boardId)
        {
            if (string.IsNullOrEmpty(domainId)) throw new ArgumentNullException(nameof(domainId));
            if (boardId == 0) throw new ArgumentOutOfRangeException(nameof(boardId), "boardId must not be 0");
            _map[domainId] = boardId;
        }

        public bool Unregister(string domainId)
        {
            if (string.IsNullOrEmpty(domainId)) return false;
            return _map.Remove(domainId);
        }

        public void Clear()
        {
            _map.Clear();
        }
    }
}
