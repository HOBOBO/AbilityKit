using UnityEngine;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    public sealed class UnityResourcesConfigBytesSource : IConfigBytesSource
    {
        public bool TryGetBytes(string key, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(key)) return false;

            var asset = Resources.Load<TextAsset>(key);
            if (asset == null) return false;

            bytes = asset.bytes;
            return bytes != null && bytes.Length > 0;
        }
    }
}
