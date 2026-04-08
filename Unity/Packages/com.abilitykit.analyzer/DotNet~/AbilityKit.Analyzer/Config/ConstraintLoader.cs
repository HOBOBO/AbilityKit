using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AbilityKit.Analyzer.Config
{
    public sealed class ConstraintLoader
    {
        private PackageConstraintsConfig _config;
        private readonly string _configPath;
        private readonly Dictionary<string, PackageConstraint> _constraintCache = new Dictionary<string, PackageConstraint>();
        private bool _isLoaded;

        public static readonly string[] SearchPaths = new[]
        {
            "Assets/Config/PackageConstraints.json",
            "Packages/com.abilitykit.analyzer/Config/PackageConstraints.json",
        };

        public ConstraintLoader()
        {
            _configPath = ResolveConfigPath();
        }

        public ConstraintLoader(string configPath)
        {
            _configPath = configPath;
            if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
            {
                LoadFromFile();
            }
        }

        private void LoadFromFile()
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<PackageConstraintsConfig>(json);
                BuildCache();
                _isLoaded = true;
            }
            catch
            {
                _config = new PackageConstraintsConfig();
                _isLoaded = true;
            }
        }

        public static string ResolveConfigPath()
        {
            foreach (var relativePath in SearchPaths)
            {
                var absolutePath = GetAbsolutePath(relativePath);
                if (File.Exists(absolutePath))
                    return absolutePath;
            }
            return null;
        }

        private static string GetAbsolutePath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            string result = null;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var unityRoot = FindUnityRoot(baseDir);
            if (unityRoot != null)
            {
                result = Path.Combine(unityRoot, relativePath);
                if (File.Exists(result))
                    return result;
            }

            var currentDir = Directory.GetCurrentDirectory();
            result = Path.Combine(currentDir, relativePath);
            if (File.Exists(result))
                return result;

            result = Path.GetFullPath(Path.Combine(currentDir, "..", relativePath));
            if (File.Exists(result))
                return result;

            return Path.Combine(currentDir, relativePath);
        }

        private static string FindUnityRoot(string baseDir)
        {
            var dir = baseDir;
            for (int i = 0; i < 10; i++)
            {
                if (string.IsNullOrEmpty(dir))
                    break;

                var assetsDir = Path.Combine(dir, "Assets");
                var packagesDir = Path.Combine(dir, "Packages");
                if (Directory.Exists(assetsDir) && Directory.Exists(packagesDir))
                    return dir;

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;
                dir = parent.FullName;
            }
            return null;
        }

        public PackageConstraintsConfig Load()
        {
            if (_isLoaded && _config != null)
                return _config;

            if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
            {
                _config = CreateDefaultConfig();
                _isLoaded = true;
                return _config;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<PackageConstraintsConfig>(json);
                BuildCache();
            }
            catch (Exception)
            {
                _config = CreateDefaultConfig();
            }

            _isLoaded = true;
            return _config;
        }

        public void Reload()
        {
            _isLoaded = false;
            _constraintCache.Clear();
            Load();
        }

        public PackageConstraint GetConstraint(string packageName)
        {
            if (!_isLoaded)
                Load();

            if (_constraintCache.TryGetValue(packageName, out var cached))
                return cached;

            var constraint = _config.GetEffectiveConstraint(packageName);
            if (constraint != null)
                _constraintCache[packageName] = constraint;

            return constraint;
        }

        private void BuildCache()
        {
            _constraintCache.Clear();
            if (_config?.Constraints == null)
                return;

            foreach (var kvp in _config.Constraints)
            {
                _constraintCache[kvp.Key] = kvp.Value;
            }
        }

        private static PackageConstraintsConfig CreateDefaultConfig()
        {
            return new PackageConstraintsConfig();
        }

        public string ConfigPath => _configPath;

        public bool IsConfigLoaded => _config != null && _isLoaded;

        public bool HasGlobalDefaultsEnabled => _config?.GlobalDefaults?.Enabled ?? false;

        public int GetForbiddenNamespaceCount() =>
            _config?.GlobalDefaults?.ForbiddenNamespaces?.Count ?? 0;
    }
}
