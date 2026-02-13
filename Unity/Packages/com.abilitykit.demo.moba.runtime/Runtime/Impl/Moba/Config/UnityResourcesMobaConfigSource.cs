using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class UnityResourcesMobaConfigSource : IMobaConfigSource
    {
        public bool TryGetText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key)) return false;

            var asset = Resources.Load<TextAsset>(key);
            if (asset == null) return false;

            text = asset.text;
            return !string.IsNullOrEmpty(text);
        }
    }
}
