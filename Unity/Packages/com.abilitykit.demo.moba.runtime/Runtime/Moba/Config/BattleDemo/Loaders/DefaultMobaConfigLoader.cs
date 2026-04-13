using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Core.Common.Log;
using UnityEngine;
using ConfigReloadResult = AbilityKit.Ability.HotReload.ConfigReloadResult;
using ConfigReloadBus = AbilityKit.Ability.HotReload.ConfigReloadBus;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    public sealed class DefaultMobaConfigLoader
    {
        private readonly IMobaConfigTableRegistry _registry;

        public DefaultMobaConfigLoader(IMobaConfigTableRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Load(MobaConfigDatabase db, IConfigSource source, string resourcesDir = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var jsonByKey = BuildJsonByKeyFromSource(db, source, resourcesDir, strict: true, out _, out _);
            db.LoadFromJsonTexts(jsonByKey, resourcesDir);
        }

        public ConfigReloadResult Reload(MobaConfigDatabase db, IConfigSource source, string resourcesDir = null)
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

        public ConfigReloadResult ReloadFromResources(MobaConfigDatabase db, string resourcesDir, bool strict)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var jsonByKey = BuildJsonByKeyFromResources(db, resourcesDir, strict, out var hasFail, out var fail);
            if (hasFail)
            {
                ConfigReloadBus.Publish(fail);
                return fail;
            }
            return db.ReloadFromJsonTexts(jsonByKey, resourcesDir);
        }

        private Dictionary<string, string> BuildJsonByKeyFromSource(
            MobaConfigDatabase db,
            IConfigSource source,
            string resourcesDir,
            bool strict,
            out bool hasFail,
            out ConfigReloadResult fail)
        {
            hasFail = false;
            fail = default;
            var jsonByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var tables = _registry.Tables;

            for (var i = 0; i < tables.Count; i++)
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

                // Validate JSON format - must be an array
                if (!ValidateJsonArrayFormat(json, t.FileWithoutExt))
                {
                    if (strict)
                    {
                        throw new InvalidOperationException($"Config json is not a valid array format: {fullPath}");
                    }
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json is not a valid array format: {fullPath}");
                    return jsonByKey;
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

            for (var i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                var path = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                // Try to load merged JSON file with explicit .json extension
                var json = TryLoadMergedJsonWithExtension(path);
                if (string.IsNullOrEmpty(json))
                {
                    json = TryLoadMergedJsonWithExtension(t.FileWithoutExt);
                }

                // Fallback: try without extension (original behavior)
                if (string.IsNullOrEmpty(json))
                {
                    json = TryLoadMergedJson(path);
                }
                if (string.IsNullOrEmpty(json))
                {
                    json = TryLoadMergedJson(t.FileWithoutExt);
                }

                if (string.IsNullOrEmpty(json))
                {
                    if (strict) throw new InvalidOperationException($"Config json not found in Resources: {path}");
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json not found in Resources: {path}");
                    return jsonByKey;
                }

                // Validate JSON format - must be an array for merged config files
                if (!ValidateJsonArrayFormat(json, t.FileWithoutExt))
                {
                    if (strict)
                    {
                        throw new InvalidOperationException($"Config json is not a valid array format: {path}. Expected array format like [{{...}}, {{...}}]");
                    }
                    hasFail = true;
                    fail = ConfigReloadResult.Fail("moba.config", db != null ? db.Version : 0, $"Config json is not a valid array format: {path}");
                    return jsonByKey;
                }

                jsonByKey[path] = json;
                jsonByKey[t.FileWithoutExt] = json;
            }

            return jsonByKey;
        }

        private string TryLoadMergedJsonWithExtension(string pathWithoutExt)
        {
            // Explicitly add .json extension to avoid loading folder
            var pathWithExt = pathWithoutExt + ".json";
            var asset = Resources.Load<TextAsset>(pathWithExt);
            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                var text = asset.text.TrimStart();
                // Must be an array format (merged JSON)
                if (text.StartsWith("["))
                {
                    return asset.text;
                }
            }
            return null;
        }

        private string TryLoadMergedJson(string path)
        {
            // Fallback: try without extension
            var asset = Resources.Load<TextAsset>(path);
            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                var text = asset.text.TrimStart();
                // Check if it's a valid array format (starts with '[')
                if (text.StartsWith("["))
                {
                    return asset.text;
                }
            }
            return null;
        }

        private bool ValidateJsonArrayFormat(string json, string fileName)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            var trimmed = json.TrimStart();
            // Valid config format is an array: [...]
            if (trimmed.StartsWith("["))
                return true;

            // If it starts with '{', it's likely Luban's individual file format
            if (trimmed.StartsWith("{"))
            {
                Log.Warning($"[ConfigLoader] File '{fileName}' contains single object format (Luban separate export). Expected merged array format.");
                return false;
            }

            Log.Warning($"[ConfigLoader] File '{fileName}' has invalid format. Expected array '[...]' but got: {trimmed.Substring(0, Math.Min(50, trimmed.Length))}...");
            return false;
        }
    }
}
