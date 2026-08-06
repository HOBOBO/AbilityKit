using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget.Providers
{
    /// <summary>
    /// 按声明顺序依次枚举各候选源，并保留重复候选。
    /// 空候选源会被跳过。
    /// </summary>
    public sealed class ConcatCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider[] _providers;

        public ConcatCandidateProvider(params ICandidateProvider[] providers)
        {
            _providers = CompositeProviderUtility.CopyProviders(providers);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            for (int i = 0; i < _providers.Length; i++)
            {
                var provider = _providers[i];
                if (provider != null)
                {
                    provider.ForEachCandidate(in query, context, ref consumer);
                }
            }
        }
    }

    /// <summary>
    /// 按稳定键枚举所有候选源的并集，并保留候选首次出现的顺序。
    /// 空候选源和空候选会被跳过。
    /// </summary>
    public sealed class UnionDistinctCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider[] _providers;

        public UnionDistinctCandidateProvider(params ICandidateProvider[] providers)
        {
            _providers = CompositeProviderUtility.CopyProviders(providers);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var keyProvider = context.EntityKeyProvider;
            var seenKeys = TargetingPool.RentEntityKeySet();
            var distinctConsumer = new DistinctForwardingConsumer<TConsumer>(consumer, keyProvider, seenKeys);
            try
            {
                for (int i = 0; i < _providers.Length; i++)
                {
                    var provider = _providers[i];
                    if (provider != null)
                    {
                        provider.ForEachCandidate(in query, context, ref distinctConsumer);
                    }
                }
            }
            finally
            {
                consumer = distinctConsumer.Consumer;
                TargetingPool.ReleaseEntityKeySet(seenKeys);
            }
        }

        private struct DistinctForwardingConsumer<TConsumer> : ICandidateConsumer
            where TConsumer : struct, ICandidateConsumer
        {
            public TConsumer Consumer;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly HashSet<ulong> _seenKeys;

            public DistinctForwardingConsumer(
                TConsumer consumer,
                IEntityKeyProvider keyProvider,
                HashSet<ulong> seenKeys)
            {
                Consumer = consumer;
                _keyProvider = keyProvider;
                _seenKeys = seenKeys;
            }

            public void Consume(EntityId id)
            {
                if (!id.IsValid) return;
                if (_seenKeys.Add(CompositeProviderUtility.GetKey(id, _keyProvider)))
                {
                    Consumer.Consume(id);
                }
            }
        }
    }

    /// <summary>
    /// 按稳定键枚举同时存在于每个候选源中的候选。
    /// 结果保持唯一，并遵循候选在首个来源中首次出现的顺序。
    /// 任一候选源为空引用时，交集为空。
    /// </summary>
    public sealed class IntersectCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider[] _providers;

        public IntersectCandidateProvider(params ICandidateProvider[] providers)
        {
            _providers = CompositeProviderUtility.CopyProviders(providers);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_providers.Length == 0 || _providers[0] == null) return;

            var keyProvider = context.EntityKeyProvider;
            var candidates = TargetingPool.RentEntityIdList();
            var sourceKeys = TargetingPool.RentEntityKeySet();
            try
            {
                var firstConsumer = new CollectDistinctConsumer(candidates, sourceKeys, keyProvider);
                _providers[0].ForEachCandidate(in query, context, ref firstConsumer);

                for (int i = 1; i < _providers.Length && candidates.Count > 0; i++)
                {
                    var provider = _providers[i];
                    if (provider == null) return;

                    sourceKeys.Clear();
                    var keyConsumer = new CollectKeysConsumer(sourceKeys, keyProvider);
                    provider.ForEachCandidate(in query, context, ref keyConsumer);
                    RetainMatching(candidates, sourceKeys, keyProvider);
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    consumer.Consume(candidates[i]);
                }
            }
            finally
            {
                TargetingPool.ReleaseEntityKeySet(sourceKeys);
                TargetingPool.ReleaseEntityIdList(candidates);
            }
        }

        private static void RetainMatching(
            List<EntityId> candidates,
            HashSet<ulong> sourceKeys,
            IEntityKeyProvider keyProvider)
        {
            var writeIndex = 0;
            for (int readIndex = 0; readIndex < candidates.Count; readIndex++)
            {
                var candidate = candidates[readIndex];
                var key = CompositeProviderUtility.GetKey(candidate, keyProvider);
                if (!sourceKeys.Contains(key)) continue;

                candidates[writeIndex++] = candidate;
            }

            if (writeIndex < candidates.Count)
            {
                candidates.RemoveRange(writeIndex, candidates.Count - writeIndex);
            }
        }
    }

    /// <summary>
    /// 枚举主候选源中稳定键未出现在任何排除源内的候选。
    /// 结果保持唯一，并遵循候选在主候选源中首次出现的顺序。
    /// 空排除源会被跳过。
    /// </summary>
    public sealed class ExceptCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider _primary;
        private readonly ICandidateProvider[] _exclusions;

        public ExceptCandidateProvider(ICandidateProvider primary, params ICandidateProvider[] exclusions)
        {
            _primary = primary;
            _exclusions = CompositeProviderUtility.CopyProviders(exclusions);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_primary == null) return;

            var keyProvider = context.EntityKeyProvider;
            var excludedKeys = TargetingPool.RentEntityKeySet();
            var emittedKeys = TargetingPool.RentEntityKeySet();
            try
            {
                var exclusionConsumer = new CollectKeysConsumer(excludedKeys, keyProvider);
                for (int i = 0; i < _exclusions.Length; i++)
                {
                    var exclusion = _exclusions[i];
                    if (exclusion != null)
                    {
                        exclusion.ForEachCandidate(in query, context, ref exclusionConsumer);
                    }
                }

                var exceptConsumer = new ExceptForwardingConsumer<TConsumer>(
                    consumer,
                    keyProvider,
                    excludedKeys,
                    emittedKeys);
                try
                {
                    _primary.ForEachCandidate(in query, context, ref exceptConsumer);
                }
                finally
                {
                    consumer = exceptConsumer.Consumer;
                }
            }
            finally
            {
                TargetingPool.ReleaseEntityKeySet(emittedKeys);
                TargetingPool.ReleaseEntityKeySet(excludedKeys);
            }
        }

        private struct ExceptForwardingConsumer<TConsumer> : ICandidateConsumer
            where TConsumer : struct, ICandidateConsumer
        {
            public TConsumer Consumer;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly HashSet<ulong> _excludedKeys;
            private readonly HashSet<ulong> _emittedKeys;

            public ExceptForwardingConsumer(
                TConsumer consumer,
                IEntityKeyProvider keyProvider,
                HashSet<ulong> excludedKeys,
                HashSet<ulong> emittedKeys)
            {
                Consumer = consumer;
                _keyProvider = keyProvider;
                _excludedKeys = excludedKeys;
                _emittedKeys = emittedKeys;
            }

            public void Consume(EntityId id)
            {
                if (!id.IsValid) return;

                var key = CompositeProviderUtility.GetKey(id, _keyProvider);
                if (!_excludedKeys.Contains(key) && _emittedKeys.Add(key))
                {
                    Consumer.Consume(id);
                }
            }
        }
    }

    internal struct CollectDistinctConsumer : ICandidateConsumer
    {
        private readonly List<EntityId> _candidates;
        private readonly HashSet<ulong> _keys;
        private readonly IEntityKeyProvider _keyProvider;

        public CollectDistinctConsumer(
            List<EntityId> candidates,
            HashSet<ulong> keys,
            IEntityKeyProvider keyProvider)
        {
            _candidates = candidates;
            _keys = keys;
            _keyProvider = keyProvider;
        }

        public void Consume(EntityId id)
        {
            if (!id.IsValid) return;
            if (_keys.Add(CompositeProviderUtility.GetKey(id, _keyProvider)))
            {
                _candidates.Add(id);
            }
        }
    }

    internal struct CollectKeysConsumer : ICandidateConsumer
    {
        private readonly HashSet<ulong> _keys;
        private readonly IEntityKeyProvider _keyProvider;

        public CollectKeysConsumer(HashSet<ulong> keys, IEntityKeyProvider keyProvider)
        {
            _keys = keys;
            _keyProvider = keyProvider;
        }

        public void Consume(EntityId id)
        {
            if (!id.IsValid) return;
            _keys.Add(CompositeProviderUtility.GetKey(id, _keyProvider));
        }
    }

    internal static class CompositeProviderUtility
    {
        public static ICandidateProvider[] CopyProviders(ICandidateProvider[] providers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            if (providers.Length == 0) return Array.Empty<ICandidateProvider>();

            var snapshot = new ICandidateProvider[providers.Length];
            Array.Copy(providers, snapshot, providers.Length);
            return snapshot;
        }

        public static ulong GetKey(EntityId id, IEntityKeyProvider keyProvider)
        {
            return keyProvider != null ? keyProvider.GetKey(id) : id.Value;
        }
    }
}
