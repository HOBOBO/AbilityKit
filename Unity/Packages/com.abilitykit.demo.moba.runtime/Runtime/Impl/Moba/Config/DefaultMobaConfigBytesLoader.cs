using System;
using System.Collections.Generic;
using AbilityKit.Ability.HotReload;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class DefaultMobaConfigBytesLoader : IMobaConfigBytesLoader
    {
        private readonly IMobaConfigTableRegistry _registry;

        public DefaultMobaConfigBytesLoader(IMobaConfigTableRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Load(MobaConfigDatabase db, IMobaConfigBytesSource source, string resourcesDir = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var bytesByKey = BuildBytesByKeyFromSource(db, source, resourcesDir, strict: true, out _, out _);
            db.LoadFromBytes(bytesByKey, resourcesDir);
        }

        public ConfigReloadResult Reload(MobaConfigDatabase db, IMobaConfigBytesSource source, string resourcesDir = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var bytesByKey = BuildBytesByKeyFromSource(db, source, resourcesDir, strict: false, out var hasFail, out var fail);
            if (hasFail)
            {
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            return db.ReloadFromBytes(bytesByKey, resourcesDir);
        }

        public void LoadFromResources(MobaConfigDatabase db, string resourcesDir)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var bytesByKey = BuildBytesByKeyFromResources(db, resourcesDir, strict: true, out _, out _);
            db.LoadFromBytes(bytesByKey, resourcesDir);
        }

        public ConfigReloadResult ReloadFromResources(MobaConfigDatabase db, string resourcesDir)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var bytesByKey = BuildBytesByKeyFromResources(db, resourcesDir, strict: false, out var hasFail, out var fail);
            if (hasFail)
            {
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            return db.ReloadFromBytes(bytesByKey, resourcesDir);
        }

        private Dictionary<string, byte[]> BuildBytesByKeyFromSource(
            MobaConfigDatabase db,
            IMobaConfigBytesSource source,
            string resourcesDir,
            bool strict,
            out bool hasFail,
            out ConfigReloadResult fail)
        {
            hasFail = false;
            fail = default;

            var bytesByKey = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var tables = _registry.Tables;

            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                if (!source.TryGetBytes(fullPath, out var bytes) || bytes == null || bytes.Length == 0)
                {
                    if (!source.TryGetBytes(t.FileWithoutExt, out bytes) || bytes == null || bytes.Length == 0)
                    {
                        if (strict)
                        {
                            throw new InvalidOperationException($"Config bytes not found in source: {fullPath}");
                        }

                        hasFail = true;
                        fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config bytes not found in source: {fullPath}");
                        return bytesByKey;
                    }
                }

                bytesByKey[fullPath] = bytes;
                bytesByKey[t.FileWithoutExt] = bytes;
            }

            return bytesByKey;
        }

        private Dictionary<string, byte[]> BuildBytesByKeyFromResources(
            MobaConfigDatabase db,
            string resourcesDir,
            bool strict,
            out bool hasFail,
            out ConfigReloadResult fail)
        {
            hasFail = false;
            fail = default;

            var bytesByKey = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var tables = _registry.Tables;

            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var path = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null)
                {
                    asset = Resources.Load<TextAsset>(t.FileWithoutExt);
                }

                if (asset == null)
                {
                    if (strict) throw new InvalidOperationException($"Config bytes not found in Resources: {path}");
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config bytes not found in Resources: {path}");
                    return bytesByKey;
                }

                var bytes = asset.bytes;
                if (bytes == null || bytes.Length == 0)
                {
                    if (strict) throw new InvalidOperationException($"Config bytes is empty: {path}");
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config bytes is empty: {path}");
                    return bytesByKey;
                }

                bytesByKey[path] = bytes;
                bytesByKey[t.FileWithoutExt] = bytes;
            }

            return bytesByKey;
        }
    }
}
