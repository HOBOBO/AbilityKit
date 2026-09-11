using System;
using AbilityKit.Samples.Abstractions;
using System.IO;

namespace AbilityKit.Samples.Infrastructure
{
    /// <summary>
    /// 文件系统资源加载器（用于独立程序/编辑器）
    /// </summary>
    public sealed class FileSystemResourceProvider : IResourceProvider
    {
        private readonly string _baseDirectory;

        public FileSystemResourceProvider() : this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        public FileSystemResourceProvider(string baseDirectory)
        {
            _baseDirectory = Path.GetFullPath(baseDirectory);
        }

        public string LoadText(string path)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Resource not found: {path}", fullPath);
            }
            return File.ReadAllText(fullPath);
        }

        public bool TryLoadText(string path, out string content)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    content = File.ReadAllText(fullPath);
                    return true;
                }
            }
            catch
            {
                // 忽略异常
            }
            content = null;
            return false;
        }

        public bool Exists(string path)
        {
            var fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }

        public string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            return Path.Combine(_baseDirectory, path);
        }
    }
}
