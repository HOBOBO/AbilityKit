using UnityEngine;

namespace AbilityKit.Game.UI
{
    public sealed class ResourcesUIAssetProvider : IUIAssetProvider
    {
        public GameObject Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return Resources.Load<GameObject>(path);
        }
    }
}
