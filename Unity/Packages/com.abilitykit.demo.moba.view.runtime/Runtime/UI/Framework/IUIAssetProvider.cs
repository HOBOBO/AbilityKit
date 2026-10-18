using UnityEngine;

namespace AbilityKit.Game.UI
{
    public interface IUIAssetProvider
    {
        GameObject Load(string path);
    }
}
