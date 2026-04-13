using UnityEngine;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    public sealed class UnityResourcesConfigSource : IConfigSource
    {
        public bool TryGetText(string path, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(path)) return false;

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return false;

            text = asset.text;
            return !string.IsNullOrEmpty(text);
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(path)) return false;

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return false;

            bytes = asset.bytes;
            return bytes != null && bytes.Length > 0;
        }
    }
}
