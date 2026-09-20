using System;
using AbilityKit.Ability.Config;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// Unity Resources 实现的 TextLoader。
    /// 内部使用 ITextAssetLoader 加载资源，保持与逻辑层的解耦。
    /// </summary>
    public sealed class UnityResourcesTextLoader : TriggerPlanJsonDatabase.ITextLoader
    {
        private readonly ITextAssetLoader _textAssetLoader;

        public UnityResourcesTextLoader(ITextAssetLoader textAssetLoader)
        {
            _textAssetLoader = textAssetLoader ?? throw new ArgumentNullException(nameof(textAssetLoader));
        }

        public bool TryLoad(string id, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id)) return false;

            if (_textAssetLoader.TryLoadText(id, out text))
            {
                return !string.IsNullOrEmpty(text);
            }

            return false;
        }
    }
}
