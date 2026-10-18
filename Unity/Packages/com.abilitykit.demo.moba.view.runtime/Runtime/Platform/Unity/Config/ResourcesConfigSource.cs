using System;
using AbilityKit.Ability.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Battle.Moba.Config.Sources
{
    public sealed class ResourcesConfigSource : IConfigSource
    {
        private readonly string _basePath;

        public ResourcesConfigSource(string basePath = null)
        {
            _basePath = basePath;
        }

        public bool TryGetText(string path, out string text)
        {
            text = null;
            var asset = ResourcesAssetProvider.Shared.Load<TextAsset>(GetFullPath(path));
            if (asset == null) return false;
            text = asset.text;
            return !string.IsNullOrEmpty(text);
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            var asset = ResourcesAssetProvider.Shared.Load<TextAsset>(GetFullPath(path));
            if (asset == null) return false;
            bytes = asset.bytes;
            return bytes != null && bytes.Length > 0;
        }

        private string GetFullPath(string path)
        {
            return string.IsNullOrEmpty(_basePath) ? path : $"{_basePath}/{path}";
        }
    }
}
