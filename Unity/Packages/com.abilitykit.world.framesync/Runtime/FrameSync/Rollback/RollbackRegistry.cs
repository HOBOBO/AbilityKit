using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackRegistry
    {
        private readonly List<IRollbackStateProvider> _providers = new List<IRollbackStateProvider>(16);
        private readonly Dictionary<int, IRollbackStateProvider> _byKey = new Dictionary<int, IRollbackStateProvider>(16);

        private bool _sealed;
        private readonly List<int> _requiredStateKeys = new List<int>();

        public IReadOnlyList<IRollbackStateProvider> Providers => _providers;
        public IReadOnlyList<int> RequiredStateKeys => _requiredStateKeys.AsReadOnly();

        /// <summary>Require exactly one state entry for a registered provider before any import.
        /// Registries without requirements retain their existing partial snapshot contract.</summary>
        public void RequireState(int key)
        {
            if (_sealed) throw new InvalidOperationException("Rollback registry is sealed.");
            if (!_byKey.ContainsKey(key)) throw new InvalidOperationException($"Required rollback provider not registered: {key}");
            if (_requiredStateKeys.Contains(key)) return;
            _requiredStateKeys.Add(key);
            _requiredStateKeys.Sort();
        }

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
            _requiredStateKeys.Clear();
            _sealed = false;
        }
    }
}
