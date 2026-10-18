using UnityEngine;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 战斗 View Runtime 的统一资源加载入口。
    /// 默认实现仍可使用 Unity Resources，后续可替换为 Addressables/AssetBundle/热更资源系统。
    /// </summary>
    public interface IAssetProvider
    {
        T Load<T>(string path) where T : Object;

        T[] LoadAll<T>(string path) where T : Object;
    }

    public interface IAssetReleaseProvider
    {
        void Release(Object asset);
    }
}
