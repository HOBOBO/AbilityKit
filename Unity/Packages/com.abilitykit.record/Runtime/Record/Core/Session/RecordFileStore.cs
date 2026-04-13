using System.IO;
using UnityEngine;

namespace AbilityKit.Core.Common.Record.Core
{
    public static class RecordFileStore
    {
        public static void Save(string path, byte[] data)
        {
            var resolved = ResolvePath(path);
            var dir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(resolved, data);
        }

        public static byte[] Load(string path)
        {
            var resolved = ResolvePath(path);
            return File.ReadAllBytes(resolved);
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return path;

            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            return Path.Combine(baseDir, path);
        }
    }
}
