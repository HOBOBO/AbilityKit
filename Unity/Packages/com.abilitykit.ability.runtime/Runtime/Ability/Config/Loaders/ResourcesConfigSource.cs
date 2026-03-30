using UnityEngine;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// Unity Resources 配置数据源
    /// </summary>
    public sealed class ResourcesConfigSource : IConfigSource
    {
        private readonly string _basePath;

        public string BasePath => _basePath;

        public ResourcesConfigSource(string basePath = null)
        {
            _basePath = basePath;
        }

        public bool TryGetText(string path, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(path)) return false;

            var fullPath = GetFullPath(path);
            var asset = Resources.Load<TextAsset>(fullPath);
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>(path);
            }

            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                text = asset.text;
                return true;
            }

            return false;
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(path)) return false;

            var fullPath = GetFullPath(path);
            var asset = Resources.Load<TextAsset>(fullPath);
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>(path);
            }

            if (asset != null && asset.bytes != null && asset.bytes.Length > 0)
            {
                bytes = asset.bytes;
                return true;
            }

            return false;
        }

        private string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(_basePath))
                return path;
            return $"{_basePath}/{path}";
        }
    }
}
