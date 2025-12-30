using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackRegistry
    {
        private readonly List<IRollbackStateProvider> _providers = new List<IRollbackStateProvider>(16);
        private readonly Dictionary<int, IRollbackStateProvider> _byKey = new Dictionary<int, IRollbackStateProvider>(16);

        public IReadOnlyList<IRollbackStateProvider> Providers => _providers;

        public void Register(IRollbackStateProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (_byKey.TryGetValue(provider.Key, out var existing) && existing != null)
            {
                if (!ReferenceEquals(existing, provider))
                {
                    throw new InvalidOperationException($"Rollback provider key already registered: {provider.Key}");
                }

                return;
            }

            _byKey[provider.Key] = provider;
            _providers.Add(provider);
        }

        public bool TryGet(int key, out IRollbackStateProvider provider)
        {
            return _byKey.TryGetValue(key, out provider);
        }

        public void Clear()
        {
            _providers.Clear();
            _byKey.Clear();
        }
    }
}
