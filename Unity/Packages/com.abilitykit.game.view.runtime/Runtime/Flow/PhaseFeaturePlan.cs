using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Foundation;

namespace AbilityKit.Game.View.Flow
{
    public sealed class PhaseFeaturePlan<TContext, TFeature>
        where TFeature : class, IPhaseFeature<TContext>
    {
        public delegate TFeature FeatureFactory(in TContext ctx);

        private readonly List<Entry> _entries;

        public PhaseFeaturePlan(int initialCapacity = 8)
        {
            _entries = new List<Entry>(Math.Max(0, initialCapacity));
        }

        public int Count => _entries.Count;

        public PhaseFeaturePlan<TContext, TFeature> Add(string id, FeatureFactory factory)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Feature id is required.", nameof(id));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _entries.Add(new Entry(id, factory));
            return this;
        }

        public int InstallAll(in TContext ctx, Action<TFeature> install)
        {
            if (install == null) throw new ArgumentNullException(nameof(install));

            var installed = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                install(_entries[i].Factory(in ctx));
                installed++;
            }

            return installed;
        }

        public int InstallByIdsOrAll(
            IReadOnlyList<string>? ids,
            in TContext ctx,
            Action<TFeature> install,
            Action<string>? fail = null)
        {
            if (install == null) throw new ArgumentNullException(nameof(install));

            if (ids == null)
            {
                return InstallAll(in ctx, install);
            }

            using var selected = ViewFrameworkPools.GetList<Entry>(ids.Count);
            var selectedEntries = selected.List;

            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;

                if (TryFind(id, out var entry))
                {
                    selectedEntries.Add(entry);
                }
                else
                {
                    fail?.Invoke($"Phase feature id not registered: {id}");
                }
            }

            for (var i = 0; i < selectedEntries.Count; i++)
            {
                install(selectedEntries[i].Factory(in ctx));
            }

            return selectedEntries.Count;
        }

        public bool TryCreate(string id, in TContext ctx, out TFeature? feature)
        {
            feature = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (!TryFind(id, out var entry)) return false;

            feature = entry.Factory(in ctx);
            return true;
        }

        private bool TryFind(string id, out Entry entry)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Id, id, StringComparison.Ordinal))
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        private readonly struct Entry
        {
            public readonly string Id;
            public readonly FeatureFactory Factory;

            public Entry(string id, FeatureFactory factory)
            {
                Id = id;
                Factory = factory;
            }
        }
    }
}
