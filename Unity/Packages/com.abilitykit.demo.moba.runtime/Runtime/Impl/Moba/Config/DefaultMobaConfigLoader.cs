using System;
using System.Collections.Generic;
using AbilityKit.Ability.HotReload;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class DefaultMobaConfigLoader : IMobaConfigLoader
    {
        private readonly IMobaConfigTableRegistry _registry;

        public DefaultMobaConfigLoader(IMobaConfigTableRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Load(MobaConfigDatabase db, IMobaConfigSource source, string resourcesDir = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var jsonByKey = BuildJsonByKeyFromSource(db, source, resourcesDir, strict: true, out _, out _);
            db.LoadFromJsonTexts(jsonByKey, resourcesDir);
        }

        public ConfigReloadResult Reload(MobaConfigDatabase db, IMobaConfigSource source, string resourcesDir = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var jsonByKey = BuildJsonByKeyFromSource(db, source, resourcesDir, strict: false, out var hasFail, out var fail);
            if (hasFail)
            {
                ConfigReloadBus.Publish(fail);
                return fail;
            }
            return db.ReloadFromJsonTexts(jsonByKey, resourcesDir);
        }

        public void LoadFromResources(MobaConfigDatabase db, string resourcesDir)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var jsonByKey = BuildJsonByKeyFromResources(db, resourcesDir, strict: true, out _, out _);
            db.LoadFromJsonTexts(jsonByKey, resourcesDir);
        }

        public ConfigReloadResult ReloadFromResources(MobaConfigDatabase db, string resourcesDir)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var jsonByKey = BuildJsonByKeyFromResources(db, resourcesDir, strict: false, out var hasFail, out var fail);
            if (hasFail)
            {
                ConfigReloadBus.Publish(fail);
                return fail;
            }
            return db.ReloadFromJsonTexts(jsonByKey, resourcesDir);
        }

        private Dictionary<string, string> BuildJsonByKeyFromSource(
            MobaConfigDatabase db,
            IMobaConfigSource source,
            string resourcesDir,
            bool strict,
            out bool hasFail,
            out ConfigReloadResult fail)
        {
            hasFail = false;
            fail = default;
            var jsonByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var tables = _registry.Tables;

            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                if (!source.TryGetText(fullPath, out var json) || string.IsNullOrEmpty(json))
                {
                    if (!source.TryGetText(t.FileWithoutExt, out json) || string.IsNullOrEmpty(json))
                    {
                        if (strict)
                        {
                            throw new InvalidOperationException($"Config json not found in source: {fullPath}");
                        }

                        hasFail = true;
                        fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json not found in source: {fullPath}");
                        return jsonByKey;
                    }
                }

                jsonByKey[fullPath] = json;
                jsonByKey[t.FileWithoutExt] = json;
            }

            return jsonByKey;
        }

        private Dictionary<string, string> BuildJsonByKeyFromResources(
            MobaConfigDatabase db,
            string resourcesDir,
            bool strict,
            out bool hasFail,
            out ConfigReloadResult fail)
        {
            hasFail = false;
            fail = default;
            var jsonByKey = new Dictionary<string, string>(StringComparer.Ordinal);
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
                    if (strict) throw new InvalidOperationException($"Config json not found in Resources: {path}");
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json not found in Resources: {path}");
                    return jsonByKey;
                }

                var json = asset.text;
                if (string.IsNullOrEmpty(json))
                {
                    if (strict) throw new InvalidOperationException($"Config json is empty: {path}");
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json is empty: {path}");
                    return jsonByKey;
                }

                jsonByKey[path] = json;
                jsonByKey[t.FileWithoutExt] = json;
            }

            return jsonByKey;
        }
    }
}
