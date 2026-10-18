using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.UI
{
    public sealed class ResourcesUIAssetProvider : IUIAssetProvider
    {
        private readonly IAssetProvider _assets;

        public ResourcesUIAssetProvider(IAssetProvider assets = null)
        {
            _assets = assets ?? ResourcesAssetProvider.Shared;
        }

        public GameObject Load(string path)
        {
            return _assets.Load<GameObject>(path);
        }
    }
}
