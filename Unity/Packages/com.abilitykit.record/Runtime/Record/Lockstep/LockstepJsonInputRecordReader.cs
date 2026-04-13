using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    public static class LockstepJsonInputRecordReader
    {
        public static LockstepInputRecordFile Load(string path)
        {
            var resolved = ResolvePath(path);
            var json = File.ReadAllText(resolved);
            return JsonConvert.DeserializeObject<LockstepInputRecordFile>(json);
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return path;

            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            return Path.Combine(baseDir, path);
        }
    }
}
