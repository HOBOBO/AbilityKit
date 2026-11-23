using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    internal sealed class ConsoleTriggerPlanTextSource : IFileSystemTextLoader
    {
        private readonly ITextAssetLoader _inner;
        private readonly string _basePath;

        public ConsoleTriggerPlanTextSource(ITextAssetLoader inner)
        {
            _inner = inner;
            _basePath = (inner as ConsoleTextAssetLoader)?.BasePath ?? string.Empty;
        }

        public bool TryLoad(string id, out string text)
        {
            var normalizedId = id;
            if (normalizedId.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                normalizedId = normalizedId.Substring(0, normalizedId.Length - 5);
            }
            return _inner.TryLoadText(normalizedId, out text);
        }

        public IEnumerable<string> GetFiles(string directory, string pattern)
        {
            if (string.IsNullOrEmpty(_basePath))
                return Enumerable.Empty<string>();

            var normalizedBasePath = _basePath.Replace('/', Path.DirectorySeparatorChar);
            var normalizedDir = directory.Replace('/', Path.DirectorySeparatorChar);
            var fullDir = Path.Combine(normalizedBasePath, normalizedDir);

            if (!Directory.Exists(fullDir)) return Enumerable.Empty<string>();

            var searchPattern = pattern.Replace("**/", "").Replace("**", "*");

            try
            {
                return Directory.GetFiles(fullDir, searchPattern, SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .Select(f => GetRelativePath(f))
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(_basePath)) return fullPath;
            var normalizedBasePath = _basePath.Replace('/', Path.DirectorySeparatorChar);
            var relative = fullPath.Replace(normalizedBasePath + Path.DirectorySeparatorChar, "");
            return relative.Replace('\\', '/');
        }

    }
}
