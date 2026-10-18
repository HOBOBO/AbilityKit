using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AbilityKit.Ability.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AbilityKit.Demo.Moba.View.Config
{
    /// <summary>
    /// Unity Resources 实现的 TextAsset 加载器。
    /// 这是 View 层实现，负责处理 Unity 平台的资源加载。
    /// </summary>
    [WorldService(typeof(ITextAssetLoader), WorldLifetime.Singleton)]
    [WorldService(typeof(ITextAssetDirectoryLoader), WorldLifetime.Singleton)]
    public sealed class ResourcesTextAssetLoader : ITextAssetLoader, ITextAssetDirectoryLoader
    {
        private readonly Dictionary<string, TextAsset> _enumeratedAssets = new Dictionary<string, TextAsset>(StringComparer.Ordinal);
        private readonly IAssetProvider _assets;

        public ResourcesTextAssetLoader(IAssetProvider assets = null)
        {
            _assets = assets ?? ResourcesAssetProvider.Shared;
        }

        public bool TryLoadText(string path, out string text)
        {
            text = null;
            var normalizedPath = NormalizeResourcePath(path);
            if (string.IsNullOrEmpty(normalizedPath)) return false;

            if (!_enumeratedAssets.TryGetValue(normalizedPath, out var asset) || asset == null)
            {
                asset = _assets.Load<TextAsset>(normalizedPath);
            }
            if (asset == null) return false;

            text = asset.text;
            return !string.IsNullOrEmpty(text);
        }

        public bool TryLoadBytes(string path, out byte[] bytes)
        {
            bytes = null;
            var normalizedPath = NormalizeResourcePath(path);
            if (string.IsNullOrEmpty(normalizedPath)) return false;

            if (!_enumeratedAssets.TryGetValue(normalizedPath, out var asset) || asset == null)
            {
                asset = _assets.Load<TextAsset>(normalizedPath);
            }
            if (asset == null) return false;

            bytes = asset.bytes;
            return bytes != null && bytes.Length > 0;
        }

        public IEnumerable<string> GetTextAssetPaths(string directory, string pattern)
        {
            var normalizedDirectory = NormalizeResourcePath(directory);
            if (string.IsNullOrEmpty(normalizedDirectory)) return Array.Empty<string>();

            var paths = new List<string>();

#if UNITY_EDITOR
            paths.AddRange(GetEditorResourcePaths(normalizedDirectory, pattern));
#endif

            var assets = _assets.LoadAll<TextAsset>(normalizedDirectory);
            if (assets != null && assets.Length > 0)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    if (asset == null || !MatchesPattern(asset.name, pattern)) continue;

                    var path = normalizedDirectory + "/" + asset.name;
                    _enumeratedAssets[path] = asset;
                    paths.Add(path);
                }
            }

            return paths.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string NormalizeResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var normalized = path.Replace('\\', '/').Trim('/');
            if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 5);
            }
            else if (normalized.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 6);
            }
            return normalized;
        }

        private static bool MatchesPattern(string assetName, string pattern)
        {
            if (string.IsNullOrEmpty(assetName)) return false;
            if (string.IsNullOrEmpty(pattern) || pattern == "*.json" || pattern == "**/*.json") return true;

            var normalizedPattern = pattern.Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(normalizedPattern);
            return string.IsNullOrEmpty(fileName) || fileName == "*" || string.Equals(assetName, fileName, StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_EDITOR
        private static IReadOnlyList<string> GetEditorResourcePaths(string normalizedDirectory, string pattern)
        {
            var result = new List<string>();
            var searchPattern = string.IsNullOrEmpty(pattern) ? "*.json" : pattern.Replace("**/", string.Empty).Replace("**", "*");

            AddEditorResourcePaths(result, Application.dataPath, "Assets", normalizedDirectory, searchPattern);

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var packagesRoot = Path.Combine(projectRoot, "Packages");
                AddEditorResourcePaths(result, packagesRoot, "Packages", normalizedDirectory, searchPattern);
            }

            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static void AddEditorResourcePaths(List<string> result, string searchRoot, string assetRoot, string normalizedDirectory, string searchPattern)
        {
            if (result == null || string.IsNullOrEmpty(searchRoot) || !Directory.Exists(searchRoot)) return;

            var resourcesRoots = Directory.GetDirectories(searchRoot, "Resources", SearchOption.AllDirectories);
            foreach (var root in resourcesRoots)
            {
                var fullDirectory = Path.Combine(root, normalizedDirectory.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(fullDirectory)) continue;

                foreach (var file in Directory.GetFiles(fullDirectory, searchPattern, SearchOption.AllDirectories))
                {
                    var assetPath = file.Replace('\\', '/');
                    var resourcesIndex = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                    if (resourcesIndex < 0) continue;

                    var relative = assetPath.Substring(resourcesIndex + "/Resources/".Length);
                    if (relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        relative = relative.Substring(0, relative.Length - 5);
                    }

                    var unityAssetPath = assetRoot + "/" + assetPath.Substring(searchRoot.Replace('\\', '/').Length + 1);
                    if (AssetDatabase.LoadAssetAtPath<TextAsset>(unityAssetPath) != null)
                    {
                        result.Add(relative);
                    }
                }
            }
        }
#endif
    }
}
