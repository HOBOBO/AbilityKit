using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterConfig(WorldContainerBuilder builder)
        {
            Debug.Log("[RegisterConfig] Entered");
            builder.TryRegister<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            // 临时使用 JSON 格式加载配置，因为 Luban 的 bin 格式与 cs-bin 代码存在兼容性问题
            builder.TryRegister<IMobaConfigFormatProvider>(WorldLifetime.Singleton, _ => DefaultMobaConfigFormatProvider.Instance);

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                Debug.Log("[MobaConfigDatabase Factory] invoked");
                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                _.TryResolve<IMobaConfigDtoDeserializer>(out var deserializer);
                _.TryResolve<IMobaConfigDtoBytesDeserializer>(out var bytesDeserializer);
                Debug.Log($"[MobaConfigDatabase Factory] registry={(registry != null ? registry.GetType().Name : "null")}, deserializer={(deserializer != null ? "set" : "null")}, bytesDeserializer={(bytesDeserializer != null ? "set" : "null")}");
                var db = new MobaConfigDatabase(registry, deserializer, bytesDeserializer);
                Debug.Log($"[MobaConfigDatabase Factory] after ctor: _tables.Count={CountTables(db)}, dbHash={db.GetHashCode()}");
                var loader = _.TryResolve<IMobaConfigLoader>(out var injected) && injected != null
                    ? injected
                    : new DefaultMobaConfigLoader(registry ?? MobaConfigRegistry.Instance);

                var bytesLoader = _.TryResolve<IMobaConfigBytesLoader>(out var injectedBytesLoader) && injectedBytesLoader != null
                    ? injectedBytesLoader
                    : new DefaultMobaConfigBytesLoader(registry ?? MobaConfigRegistry.Instance);
                try
                {
                    var format = _.TryResolve<IMobaConfigFormatProvider>(out var fp) && fp != null
                        ? fp.Format
                        : DefaultMobaConfigFormatProvider.Instance.Format;

                    if (format == MobaConfigFormat.Bytes)
                    {
                        _.TryResolve<IMobaConfigSource>(out var jsource);
                        _.TryResolve<IMobaConfigTextSink>(out var sink);

                        var jsonByKey = BuildPartialJsonByKey(jsource, sink, MobaConfigPaths.DefaultResourcesDir, registry ?? MobaConfigRegistry.Instance);

                        db.LoadFromJsonTexts(
                            jsonByKey,
                            MobaConfigPaths.DefaultResourcesDir,
                            strict: false);
                    }
                    else
                    {
                        if (_.TryResolve<IMobaConfigSource>(out var source) && source != null)
                        {
                            Debug.Log("[MobaConfigDatabase Factory] Loading from IMobaConfigSource");
                            var result = loader.Reload(db, source, MobaConfigPaths.DefaultResourcesDir);
                            Debug.Log($"[MobaConfigDatabase Factory] Reload result: Succeeded={result.Succeeded}, Error={result.Error ?? "null"}");
                            if (!result.Succeeded)
                            {
                                Log.Warning($"[MobaWorldBootstrapModule] Json source loading failed, falling back. err={result.Error}");
                                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                            }
                        }
                        else if (_.TryResolve<IMobaConfigTextSink>(out var sink) && sink != null)
                        {
                            Debug.Log("[MobaConfigDatabase Factory] Loading from IMobaConfigTextSink");
                            var result = loader.Reload(db, new MobaConfigSourceAdapter(sink), MobaConfigPaths.DefaultResourcesDir);
                            Debug.Log($"[MobaConfigDatabase Factory] Reload (TextSink) result: Succeeded={result.Succeeded}, Error={result.Error ?? "null"}");
                            if (!result.Succeeded)
                            {
                                Log.Warning($"[MobaWorldBootstrapModule] Json sink loading failed, falling back. err={result.Error}");
                                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                            }
                        }
                        else
                        {
                            Debug.Log("[MobaConfigDatabase Factory] No source/sink registered, loading from Resources");
                            db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                        }
                    }

                    Debug.Log($"[MobaConfigDatabase Factory] completed: CountTables={CountTables(db)}, dbHash={db.GetHashCode()}");
                    return db;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Log.Exception(ex, "[MobaWorldBootstrapModule] MobaConfigDatabase load failed");
                    throw;
                }
            });

            Debug.Log("[RegisterConfig] MobaConfigDatabase registered");
            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());
        }

        private static int CountTables(MobaConfigDatabase db)
        {
            var field = typeof(MobaConfigDatabase).GetField("_tables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(db) is Dictionary<Type, object> tables)
                return tables.Count;
            return -1;
        }

        private static Dictionary<string, byte[]> BuildPartialBytesByKey(
            IMobaConfigBytesSource source,
            string resourcesDir,
            IMobaConfigTableRegistry registry)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (registry == null) return result;

            var tables = registry.MobaTables;
            for (int i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                byte[] bytes = null;
                if (source != null)
                {
                    source.TryGetBytes(fullPath, out bytes);
                    if (bytes == null || bytes.Length == 0)
                    {
                        source.TryGetBytes(t.FileWithoutExt, out bytes);
                    }
                }

                if (bytes == null || bytes.Length == 0)
                {
                    var asset = Resources.Load<TextAsset>(fullPath) ?? Resources.Load<TextAsset>(t.FileWithoutExt);
                    if (asset != null) bytes = asset.bytes;
                }

                if (bytes == null || bytes.Length == 0) continue;
                result[fullPath] = bytes;
                result[t.FileWithoutExt] = bytes;
            }

            return result;
        }

        private static Dictionary<string, string> BuildPartialJsonByKey(
            IMobaConfigSource source,
            IMobaConfigTextSink sink,
            string resourcesDir,
            IMobaConfigTableRegistry registry)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (registry == null) return result;

            var tables = registry.MobaTables;
            for (int i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                string json = null;
                if (source != null)
                {
                    source.TryGetText(fullPath, out json);
                    if (string.IsNullOrEmpty(json)) source.TryGetText(t.FileWithoutExt, out json);
                }

                if (string.IsNullOrEmpty(json) && sink != null)
                {
                    sink.TryGetText(fullPath, out json);
                    if (string.IsNullOrEmpty(json)) sink.TryGetText(t.FileWithoutExt, out json);
                }

                if (string.IsNullOrEmpty(json))
                {
                    var asset = Resources.Load<TextAsset>(fullPath) ?? Resources.Load<TextAsset>(t.FileWithoutExt);
                    if (asset != null) json = asset.text;
                }

                if (string.IsNullOrEmpty(json)) continue;
                result[fullPath] = json;
                result[t.FileWithoutExt] = json;
            }

            return result;
        }

        private static bool IsMissingConfigResource(InvalidOperationException ex)
        {
            if (ex == null) return false;
            var msg = ex.Message ?? string.Empty;
            return msg.IndexOf("Config bytes not found", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Config bytes is empty", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Config json not found", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Resources", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
