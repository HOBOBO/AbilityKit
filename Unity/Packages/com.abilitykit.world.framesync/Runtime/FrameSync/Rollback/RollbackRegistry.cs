using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackRegistry
    {
        private readonly List<IRollbackStateProvider> _providers = new List<IRollbackStateProvider>(16);
        private readonly Dictionary<int, IRollbackStateProvider> _byKey = new Dictionary<int, IRollbackStateProvider>(16);

        private bool _sealed;

        public IReadOnlyList<IRollbackStateProvider> Providers => _providers;

        public void Seal()
        {
            _sealed = true;
        }

        public void Register(IRollbackStateProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (_sealed)
            {
                throw new InvalidOperationException($"Rollback registry is sealed. Cannot register provider key={provider.Key}");
            }

            if (_byKey.TryGetValue(provider.Key, out var existing) && existing != null)
            {
                if (!ReferenceEquals(existing, provider))
                {
                    throw new InvalidOperationException($"Rollback provider key already registered: {provider.Key}");
                }

                return;
            }

            _byKey[provider.Key] = provider;

            var insertIndex = _providers.Count;
            for (int i = 0; i < _providers.Count; i++)
            {
                var p = _providers[i];
                if (p == null) continue;
                if (provider.Key < p.Key)
                {
                    insertIndex = i;
                    break;
                }
            }

            _providers.Insert(insertIndex, provider);
        }

        public bool TryGet(int key, out IRollbackStateProvider provider)
        {
            return _byKey.TryGetValue(key, out provider);
        }

        public void Clear()
        {
            _providers.Clear();
            _byKey.Clear();
            _sealed = false;
        }
    }
}
