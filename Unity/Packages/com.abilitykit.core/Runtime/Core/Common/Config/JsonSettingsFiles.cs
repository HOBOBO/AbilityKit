using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Core.Common.Config
{
    public static class JsonSettingsFiles
    {
        public static FlatJsonSettings LoadFlatOrEmpty(string filePath, Func<string, Dictionary<string, object>> deserialize)
        {
            if (string.IsNullOrEmpty(filePath)) return FlatJsonSettings.Empty();
            try
            {
                var full = Path.GetFullPath(filePath);
                if (!File.Exists(full)) return FlatJsonSettings.Empty();
                var json = File.ReadAllText(full);
                return FlatJsonSettings.FromJson(json, deserialize);
            }
            catch
            {
                return FlatJsonSettings.Empty();
            }
        }

        public static bool TrySaveOverrides(string filePath, IReadOnlyDictionary<string, object> overrides, Func<IReadOnlyDictionary<string, object>, string> serialize)
        {
            if (serialize == null) return false;
            if (string.IsNullOrEmpty(filePath)) return false;

            try
            {
                var full = Path.GetFullPath(filePath);
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = serialize(overrides ?? new Dictionary<string, object>(0));
                if (json == null) json = string.Empty;
                File.WriteAllText(full, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
