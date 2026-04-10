using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Core.Common.Config
{
    public static class PersistentJsonConfigLoader
    {
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly string Path;
            public readonly Type Type;

            public CacheKey(string path, Type type)
            {
                Path = path;
                Type = type;
            }

            public bool Equals(CacheKey other) => string.Equals(Path, other.Path) && Type == other.Type;
            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Path, Type);
        }

        private sealed class CacheEntry
        {
            public DateTime LastWriteTimeUtc;
            public object Value;
        }

        private static readonly object s_gate = new object();
        private static readonly Dictionary<CacheKey, CacheEntry> s_cache = new Dictionary<CacheKey, CacheEntry>(16);

        public static T LoadOrDefault<T>(string filePath, Func<string, T> deserialize) where T : class, new()
        {
            var v = TryLoad<T>(filePath, deserialize);
            return v ?? new T();
        }

        public static T TryLoad<T>(string filePath, Func<string, T> deserialize) where T : class
        {
            try
            {
                if (deserialize == null) return null;
                if (string.IsNullOrEmpty(filePath)) return null;

                var path = Path.GetFullPath(filePath);
                if (!File.Exists(path)) return null;

                var lastWrite = File.GetLastWriteTimeUtc(path);

                var key = new CacheKey(path, typeof(T));
                lock (s_gate)
                {
                    if (s_cache.TryGetValue(key, out var entry) && entry != null)
                    {
                        if (entry.LastWriteTimeUtc == lastWrite)
                        {
                            return entry.Value as T;
                        }
                    }
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;

                var v = deserialize(json);
                lock (s_gate)
                {
                    s_cache[key] = new CacheEntry
                    {
                        LastWriteTimeUtc = lastWrite,
                        Value = v,
                    };
                }
                return v;
            }
            catch
            {
                return null;
            }
        }

        public static void ClearCache()
        {
            lock (s_gate)
            {
                s_cache.Clear();
            }
        }
    }
}
