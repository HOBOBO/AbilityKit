using System;
using System.Collections.Generic;
using AbilityKit.Ability.Battle.EntityManager;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.EntityManager
{
    public sealed class KeyedEntityIndexAdapter<TKey> : IEntityIdCollectionIndex
    {
        private readonly IKeyedEntityIndex<TKey, int> _index;
        private readonly Func<int, TKey> _decodeKey;

        public KeyedEntityIndexAdapter(IKeyedEntityIndex<TKey, int> index, Func<int, TKey> decodeKey)
        {
            _index = index;
            _decodeKey = decodeKey;
        }

        public bool ForEach<TConsumer>(int key, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_index == null || _decodeKey == null) return false;

            var typedKey = _decodeKey(key);
            if (!_index.TryGet(typedKey, out var raw) || raw == null || raw.Count == 0) return false;

            // Fast paths: avoid interface-based enumeration allocations.
            if (raw is HashSet<int> set)
            {
                foreach (var id in set)
                {
                    consumer.Consume(new EcsEntityId(id));
                }

                return true;
            }

            if (raw is List<int> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    consumer.Consume(new EcsEntityId(list[i]));
                }

                return true;
            }

            foreach (var id in raw)
            {
                consumer.Consume(new EcsEntityId(id));
            }

            return true;
        }
    }
}
