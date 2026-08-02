using UnityEngine;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 基于 Unity Resources 的默认资源加载实现，作为正式资源管线接入前的兼容适配器。
    /// </summary>
    public sealed class ResourcesAssetProvider : IAssetProvider, IAssetReleaseProvider
    {
        public static readonly ResourcesAssetProvider Shared = new ResourcesAssetProvider();

        public T Load<T>(string path) where T : Object
        {
            return string.IsNullOrEmpty(path) ? null : Resources.Load<T>(path);
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            return string.IsNullOrEmpty(path) ? System.Array.Empty<T>() : Resources.LoadAll<T>(path);
        }

        public void Release(Object asset)
        {
            if (asset == null || asset is GameObject || asset is Component) return;
            Resources.UnloadAsset(asset);
        }
    }
}
