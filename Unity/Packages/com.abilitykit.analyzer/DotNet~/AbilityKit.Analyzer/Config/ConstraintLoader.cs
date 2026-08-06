using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbilityKit.Analyzer.Config
{
    public sealed class ConstraintLoader
    {
        private PackageConstraintsConfig _config;
        private readonly string _configPath;
        private readonly Dictionary<string, PackageConstraint> _constraintCache = new Dictionary<string, PackageConstraint>();
        private bool _isLoaded;

        // Unity Packages 搜索路径
        private static readonly string[] UnitySearchPaths = new[]
        {
            "Assets/Config/PackageConstraints.json",
            "Packages/com.abilitykit.analyzer/Config/PackageConstraints.json",
        };

        // src 目录构建时的搜索路径
        private static readonly string[] SrcSearchPaths = new[]
        {
            "src/Config/PackageConstraints.json",
            "src/AbilityKit.Analyzer/Config/PackageConstraints.json",
            "Configs/PackageConstraints.json",
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
                _config = ConstraintJson.DeserializeConfig(json);
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
            // 先尝试 Unity Packages 路径
            foreach (var relativePath in UnitySearchPaths)
            {
                var absolutePath = GetAbsolutePath(relativePath, FindUnityRoot);
                if (File.Exists(absolutePath))
                    return absolutePath;
            }

            // 再尝试 src 目录路径
            foreach (var relativePath in SrcSearchPaths)
            {
                var absolutePath = GetAbsolutePath(relativePath, FindSrcRoot);
                if (File.Exists(absolutePath))
                    return absolutePath;
            }

            return null;
        }

        private static string GetAbsolutePath(string relativePath, Func<string, string> rootFinder)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            // 从 AppDomain.BaseDirectory 向上查找
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var root = rootFinder(baseDir);
            if (root != null)
            {
                var result = Path.Combine(root, relativePath);
                if (File.Exists(result))
                    return result;
            }

            // 从当前目录查找
            var currentDir = Directory.GetCurrentDirectory();
            var result2 = Path.Combine(currentDir, relativePath);
            if (File.Exists(result2))
                return result2;

            // 尝试向上查找
            var parent = Directory.GetParent(currentDir);
            while (parent != null)
            {
                var result3 = Path.Combine(parent.FullName, relativePath);
                if (File.Exists(result3))
                    return result3;
                parent = Directory.GetParent(parent.FullName);
            }

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

        private static string FindSrcRoot(string baseDir)
        {
            var dir = baseDir;
            for (int i = 0; i < 10; i++)
            {
                if (string.IsNullOrEmpty(dir))
                    break;

                var srcDir = Path.Combine(dir, "src");
                var slnFile = Path.Combine(dir, "AbilityKit.sln");
                if (Directory.Exists(srcDir) && File.Exists(slnFile))
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
                _config = ConstraintJson.DeserializeConfig(json);
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
