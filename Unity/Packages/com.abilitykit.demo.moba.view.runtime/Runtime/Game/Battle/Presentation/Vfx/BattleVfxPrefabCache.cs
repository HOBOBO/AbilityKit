using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxPrefabCache
    {
        private readonly Dictionary<string, GameObject> _prefabs;
        private readonly BattleVfxResourcePrefabLoader _loader;
        private readonly IBattleAssetLookup _assets;

        public BattleVfxPrefabCache(
            BattleVfxResourcePrefabLoader loader = null,
            Dictionary<string, GameObject> prefabs = null,
            IBattleAssetLookup assets = null)
        {
            _loader = loader ?? new BattleVfxResourcePrefabLoader();
            _prefabs = prefabs ?? new Dictionary<string, GameObject>(StringComparer.Ordinal);
            _assets = assets;
        }

        public bool TryGetPrefab(string resource, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(resource))
            {
                return false;
            }

            if (!_prefabs.TryGetValue(resource, out prefab) || prefab == null)
            {
                if (_assets != null)
                {
                    prefab = _assets.TryGetAsset(resource, out var asset) ? asset as GameObject : null;
                }
                else
                {
                    prefab = _loader.Load(resource);
                }
                _prefabs[resource] = prefab;
            }

            return prefab != null;
        }

        public void Clear() => _prefabs.Clear();
    }

    internal sealed class BattleVfxResourcePrefabLoader
    {
        public GameObject Load(string resource)
        {
            return AbilityKit.Game.Battle.Shared.Assets.ResourcesAssetProvider.Shared.Load<GameObject>(resource);
        }
    }
}
