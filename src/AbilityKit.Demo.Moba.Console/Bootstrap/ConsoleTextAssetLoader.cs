using System;
using System.IO;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    /// <summary>
    /// Console 环境实现的 TextAsset 加载器。
    /// 从文件系统加载配置，支持 JSON 等文本资源。
    /// 通过 [WorldService] 属性自动注册到 World。
    /// </summary>
    [WorldService(typeof(ITextAssetLoader), WorldLifetime.Singleton)]
    public sealed class ConsoleTextAssetLoader : ITextAssetLoader
    {
        private readonly string _basePath;

        public ConsoleTextAssetLoader() : this(GetDefaultBasePath())
        {
        }

        public ConsoleTextAssetLoader(string basePath)
        {
            _basePath = string.IsNullOrEmpty(basePath) ? GetDefaultBasePath() : basePath;
        }

        public bool TryLoadText(string path, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(path)) return false;

            var fullPath = GetFullPath(path);
            Platform.Log.System($"[ConsoleTextAssetLoader] TryLoadText: path={path}, fullPath={fullPath}, exists={File.Exists(fullPath)}");

            if (!File.Exists(fullPath))
            {
                return false;
            }

            try
            {
                text = File.ReadAllText(fullPath);
                return !string.IsNullOrEmpty(text);
            }
            catch
            {
                return false;
            }
        }

        public bool TryLoadBytes(string path, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(path)) return false;

            var fullPath = GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                Platform.Log.Debug($"[ConsoleTextAssetLoader] File not found: {fullPath}");
                return false;
            }

            try
            {
                bytes = File.ReadAllBytes(fullPath);
                return bytes != null && bytes.Length > 0;
            }
            catch (Exception ex)
            {
                Platform.Log.Warn($"[ConsoleTextAssetLoader] Failed to read bytes: {fullPath}, Error: {ex.Message}");
                return false;
            }
        }

        private string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return _basePath;

            // 规范化路径分隔符（将 / 替换为 \ 以兼容 Windows）
            var normalizedPath = path.Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalizedPath))
            {
                return normalizedPath;
            }
            // 组合 _basePath 和路径
            var combined = Path.Combine(_basePath, normalizedPath);
            // 添加 .json 扩展名（文件名本身包含 .json）
            if (!combined.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !combined.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                combined += ".json";
            }
            return combined;
        }

        private static string GetDefaultBasePath()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var configDir = Path.Combine(exeDir, "Configs");
            if (Directory.Exists(configDir))
            {
                return configDir;
            }

            // 尝试向上查找项目根目录
            var current = new DirectoryInfo(exeDir);
            while (current != null)
            {
                var testPath = Path.Combine(current.FullName, "Configs");
                if (Directory.Exists(testPath))
                {
                    return testPath;
                }
                current = current.Parent;
            }

            // 最后尝试从项目目录结构查找
            var projectRoot = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "AbilityKit.Demo.Moba.Console", "Configs");
        }
    }
}
