using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleViewVfxPrefabLoader
    {
        GameObject Load(string path);
    }

    internal sealed class ResourcesBattleViewVfxPrefabLoader : IBattleViewVfxPrefabLoader
    {
        public GameObject Load(string path)
        {
            return ResourcesAssetProvider.Shared.Load<GameObject>(path);
        }
    }

    internal sealed class BattleAssetViewVfxPrefabLoader : IBattleViewVfxPrefabLoader
    {
        private readonly IBattleAssetLookup _assets;

        public BattleAssetViewVfxPrefabLoader(IBattleAssetLookup assets)
        {
            _assets = assets ?? throw new System.ArgumentNullException(nameof(assets));
        }

        public GameObject Load(string path)
        {
            return _assets.TryGetAsset(path, out var asset) ? asset as GameObject : null;
        }
    }

    internal sealed class BattleViewVfxFactory
    {
        private readonly BattleViewPrimitiveFactory _primitives;
        private readonly IBattleViewVfxPrefabLoader _loader;

        public BattleViewVfxFactory(
            BattleViewPrimitiveFactory primitives = null,
            IBattleViewVfxPrefabLoader loader = null)
        {
            _primitives = primitives ?? new BattleViewPrimitiveFactory();
            _loader = loader ?? new ResourcesBattleViewVfxPrefabLoader();
        }

        public GameObject CreateAoeVfx(VfxDatabase db, int vfxId)
        {
            if (vfxId <= 0) return null;

            if (db == null || !db.TryGet(vfxId, out var dto) || dto == null || string.IsNullOrEmpty(dto.Resource))
            {
                if (BattleViewPlaceholderIds.IsPlaceholderVfx(vfxId))
                {
                    var fallback = _primitives.CreateVfxFallback(vfxId);
                    if (fallback != null) fallback.name = $"AoeVfx_{vfxId}";
                    return fallback;
                }

                BattleViewFallbackPolicy.AllowFallback("vfx.config:" + vfxId);
                return null;
            }

            var prefab = _loader.Load(dto.Resource);
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
            }
            else
            {
                go = _primitives.CreateVfxFallback(vfxId);
            }

            if (go != null) go.name = $"AoeVfx_{vfxId}";
            return go;
        }
    }
}
