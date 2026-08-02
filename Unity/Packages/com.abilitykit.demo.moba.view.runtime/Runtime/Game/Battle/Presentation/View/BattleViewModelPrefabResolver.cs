using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleViewModelPrefabLoader
    {
        GameObject Load(string path);
    }

    internal sealed class ResourcesBattleViewModelPrefabLoader : IBattleViewModelPrefabLoader
    {
        public GameObject Load(string path)
        {
            return ResourcesAssetProvider.Shared.Load<GameObject>(path);
        }
    }

    internal sealed class BattleAssetViewModelPrefabLoader : IBattleViewModelPrefabLoader
    {
        private readonly IBattleAssetLookup _assets;

        public BattleAssetViewModelPrefabLoader(IBattleAssetLookup assets)
        {
            _assets = assets ?? throw new System.ArgumentNullException(nameof(assets));
        }

        public GameObject Load(string path)
        {
            return _assets.TryGetAsset(path, out var asset) ? asset as GameObject : null;
        }
    }

    internal sealed class BattleViewModelPrefabResolver
    {
        private readonly IBattleViewModelPrefabLoader _loader;

        public BattleViewModelPrefabResolver(IBattleViewModelPrefabLoader loader = null)
        {
            _loader = loader ?? new ResourcesBattleViewModelPrefabLoader();
        }

        public BattleViewModelPrefab Resolve(MobaConfigDatabase configs, int modelId)
        {
            if (configs == null || modelId <= 0) return default;

            try
            {
                var model = configs.GetModel(modelId);
                if (model == null) return default;

                var prefab = _loader.Load(model.PrefabPath);
                return new BattleViewModelPrefab(prefab, model.Scale);
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex);
                return default;
            }
        }
    }
}
