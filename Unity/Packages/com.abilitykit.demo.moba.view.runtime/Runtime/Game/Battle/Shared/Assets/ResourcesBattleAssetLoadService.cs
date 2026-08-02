using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// Expands Resources-backed model and VFX catalogs into concrete prefab entries.
    /// Unity automatically retains transitive material/mesh dependencies with each prefab.
    /// </summary>
    public sealed class ResourcesBattleAssetDependencyProvider : IBattleAssetDependencyProvider
    {
        private const string ModelsPath = "moba/models";
        private const string VfxPath = "vfx/vfx";

        public static readonly ResourcesBattleAssetDependencyProvider Default =
            new ResourcesBattleAssetDependencyProvider(ResourcesAssetProvider.Shared);

        private readonly IAssetProvider _provider;
        private readonly object _gate = new object();
        private IReadOnlyList<BattleAssetEntry> _cached;

        public ResourcesBattleAssetDependencyProvider(IAssetProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyList<BattleAssetEntry> ResolveDependencies(IBattleAssetManifestSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            lock (_gate)
            {
                if (_cached == null)
                {
                    _cached = BuildDependencies();
                }

                return _cached;
            }
        }

        private IReadOnlyList<BattleAssetEntry> BuildDependencies()
        {
            var entries = new List<BattleAssetEntry>();
            AddModelDependencies(LoadRequiredText(ModelsPath), entries);
            AddVfxDependencies(LoadRequiredText(VfxPath), entries);
            entries.Sort(DependencyEntryComparer.Instance);
            return entries.ToArray();
        }

        private TextAsset LoadRequiredText(string path)
        {
            var asset = _provider.Load<TextAsset>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Required presentation catalog is missing: " + path);
            }

            return asset;
        }

        private static void AddModelDependencies(TextAsset asset, List<BattleAssetEntry> entries)
        {
            var root = JsonUtility.FromJson<ModelCatalogRoot>("{\"Items\":" + asset.text + "}");
            var items = root?.Items;
            if (items == null)
            {
                throw new InvalidOperationException("Invalid model presentation catalog: " + ModelsPath);
            }

            var seen = new Dictionary<int, string>();
            for (var i = 0; i < items.Length; i++)
            {
                AddDependency(
                    entries,
                    seen,
                    items[i].Id,
                    items[i].PrefabPath,
                    "presentation:model-prefab:");
            }
        }

        private static void AddVfxDependencies(TextAsset asset, List<BattleAssetEntry> entries)
        {
            var root = JsonUtility.FromJson<VfxCatalogRoot>("{\"Items\":" + asset.text + "}");
            var items = root?.Items;
            if (items == null)
            {
                throw new InvalidOperationException("Invalid VFX presentation catalog: " + VfxPath);
            }

            var seen = new Dictionary<int, string>();
            for (var i = 0; i < items.Length; i++)
            {
                AddDependency(
                    entries,
                    seen,
                    items[i].Id,
                    items[i].Resource,
                    "presentation:vfx-prefab:");
            }
        }

        private static void AddDependency(
            List<BattleAssetEntry> entries,
            IDictionary<int, string> seen,
            int id,
            string path,
            string keyPrefix)
        {
            if (id <= 0)
            {
                throw new InvalidOperationException("Presentation dependency id must be greater than zero.");
            }

            path = path?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Presentation dependency path is missing: " + keyPrefix + id);
            }

            if (seen.TryGetValue(id, out var existing))
            {
                if (!string.Equals(existing, path, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Presentation dependency id maps to multiple paths: " + keyPrefix + id);
                }

                return;
            }

            seen.Add(id, path);
            entries.Add(new BattleAssetEntry(
                path,
                keyPrefix + id,
                BattleAssetKind.Presentation));
        }

        [Serializable]
        private sealed class ModelCatalogRoot
        {
            public ModelCatalogEntry[] Items = Array.Empty<ModelCatalogEntry>();
        }

        [Serializable]
        private sealed class ModelCatalogEntry
        {
            public int Id = 0;
            public string PrefabPath = string.Empty;
        }

        [Serializable]
        private sealed class VfxCatalogRoot
        {
            public VfxCatalogEntry[] Items = Array.Empty<VfxCatalogEntry>();
        }

        [Serializable]
        private sealed class VfxCatalogEntry
        {
            public int Id = 0;
            public string Resource = string.Empty;
        }

        private sealed class DependencyEntryComparer : IComparer<BattleAssetEntry>
        {
            public static readonly DependencyEntryComparer Instance = new DependencyEntryComparer();

            public int Compare(BattleAssetEntry x, BattleAssetEntry y)
            {
                var key = string.CompareOrdinal(x.AssetKey, y.AssetKey);
                return key != 0 ? key : string.CompareOrdinal(x.AssetPath, y.AssetPath);
            }
        }
    }

    /// <summary>
    /// 基于 <see cref="IAssetProvider"/>（默认 <see cref="ResourcesAssetProvider"/>）的
    /// <see cref="IBattleAssetSource"/> 桥接实现。将 UnityEngine 资源加载适配为纯 C# 抽象。
    /// </summary>
    public sealed class ResourcesBattleAssetSource : IBattleAssetSource, IBattleAssetReleaseSource
    {
        private readonly IAssetProvider _provider;

        public ResourcesBattleAssetSource(IAssetProvider provider)
        {
            _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));
        }

        public bool TryLoad(string path, out object asset)
        {
            if (string.IsNullOrEmpty(path))
            {
                asset = null;
                return false;
            }

            // 优先按 TextAsset 加载（配置表 / JSON 资源），失败则尝试通用 Object。
            var text = _provider.Load<TextAsset>(path);
            if (text != null)
            {
                asset = text;
                return true;
            }

            var obj = _provider.Load<UnityEngine.Object>(path);
            if (obj != null)
            {
                asset = obj;
                return true;
            }

            asset = null;
            return false;
        }

        public void Release(object asset)
        {
            if (asset is UnityEngine.Object unityAsset && _provider is IAssetReleaseProvider releasable)
            {
                releasable.Release(unityAsset);
            }
        }
    }

    /// <summary>
    /// 基于 Unity Resources 的 <see cref="IBattleAssetLoadService"/> 适配器。
    /// 包装 <see cref="BattleAssetLoadService"/>，使用 <see cref="ResourcesAssetProvider.Shared"/>。
    /// </summary>
    public sealed class ResourcesBattleAssetLoadService : IBattleAssetLoadService
    {
        /// <summary>默认单例，使用 <see cref="ResourcesAssetProvider.Shared"/>。</summary>
        public static readonly ResourcesBattleAssetLoadService Default =
            new ResourcesBattleAssetLoadService(ResourcesAssetProvider.Shared);

        private readonly BattleAssetLoadService _inner;

        public ResourcesBattleAssetLoadService(IAssetProvider provider)
        {
            _inner = new BattleAssetLoadService(new ResourcesBattleAssetSource(provider));
        }

        public System.Threading.Tasks.Task<BattleAssetLoadResult> LoadAsync(
            BattleAssetManifest manifest,
            System.IProgress<BattleAssetLoadProgress> progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return _inner.LoadAsync(manifest, progress, cancellationToken);
        }
    }
}
