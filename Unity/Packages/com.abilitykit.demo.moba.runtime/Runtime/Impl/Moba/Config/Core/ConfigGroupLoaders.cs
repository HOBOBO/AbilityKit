using System;
using UnityEngine;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// Luban 二进制格式配置组加载器，从 Resources/Bytes/ 目录加载
    /// </summary>
    public sealed class ResourcesBytesConfigGroupLoader : IConfigGroupLoader
    {
        private readonly string _resourcesDir;

        public string ResourcesDir => _resourcesDir;

        public ResourcesBytesConfigGroupLoader(string resourcesDir = "moba_bytes")
        {
            _resourcesDir = resourcesDir;
        }

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;

            var path = string.IsNullOrEmpty(_resourcesDir)
                ? tableName
                : $"{_resourcesDir}/{tableName}";

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>(tableName);
            }

            if (asset != null && asset.bytes != null && asset.bytes.Length > 0)
            {
                bytes = asset.bytes;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 传统 JSON 格式配置组加载器，从 Resources/Json/ 目录加载
    /// </summary>
    public sealed class ResourcesTextConfigGroupLoader : IConfigGroupLoader
    {
        private readonly string _resourcesDir;

        public string ResourcesDir => _resourcesDir;

        public ResourcesTextConfigGroupLoader(string resourcesDir = "moba")
        {
            _resourcesDir = resourcesDir;
        }

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;

            var path = string.IsNullOrEmpty(_resourcesDir)
                ? tableName
                : $"{_resourcesDir}/{tableName}";

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                asset = Resources.Load<TextAsset>(tableName);
            }

            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                text = asset.text;
                return true;
            }

            return false;
        }
    }
}
