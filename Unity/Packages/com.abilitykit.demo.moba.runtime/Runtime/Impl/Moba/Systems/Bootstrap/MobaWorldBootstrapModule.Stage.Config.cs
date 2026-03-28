using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
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
            builder.TryRegister<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            // 临时使用 JSON 格式加载配置，因为 Luban 的 bin 格式与 cs-bin 代码存在兼容性问题
            builder.TryRegister<IMobaConfigFormatProvider>(WorldLifetime.Singleton, _ => DefaultMobaConfigFormatProvider.Instance);

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                _.TryResolve<IMobaConfigDtoDeserializer>(out var deserializer);
                _.TryResolve<IMobaConfigDtoBytesDeserializer>(out var bytesDeserializer);
                var db = new MobaConfigDatabase(registry, deserializer, bytesDeserializer);
                var loader = _.TryResolve<IMobaConfigLoader>(out var injected) && injected != null
                    ? injected
                    : new DefaultMobaConfigLoader(registry ?? MobaConfigRegistry.Instance);

                var bytesLoader = _.TryResolve<IMobaConfigBytesLoader>(out var injectedBytesLoader) && injectedBytesLoader != null
                    ? injectedBytesLoader
                    : new DefaultMobaConfigBytesLoader(registry ?? MobaConfigRegistry.Instance);
                try
                {
                    /*
                     * 职责边界/数据流：
                     * - MobaConfigDatabase 是逻辑层“读表后的配置数据库”，供技能/BUFF/召唤/初始化等系统查询。
                     * - 接入方只需提供 IMobaConfigTextSink（表数据读取 sink），逻辑层负责解析与建库。
                     * - 若未提供 sink，则 fallback 到 Unity Resources（保持旧行为）。
                     */
                    var format = _.TryResolve<IMobaConfigFormatProvider>(out var fp) && fp != null
                        ? fp.Format
                        : DefaultMobaConfigFormatProvider.Instance.Format;

                    // 统一使用 Luban JSON 模式加载，避免混合模式
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
                            var result = loader.Reload(db, source, MobaConfigPaths.DefaultResourcesDir);
                            if (!result.Succeeded)
                            {
                                Log.Warning($"[MobaWorldBootstrapModule] Json source loading failed, falling back to tolerant Resources loading. err={result.Error}");
                                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                            }
                        }
                        else if (_.TryResolve<IMobaConfigTextSink>(out var sink) && sink != null)
                        {
                            var result = loader.Reload(db, new MobaConfigTextSinkAdapter(sink), MobaConfigPaths.DefaultResourcesDir);
                            if (!result.Succeeded)
                            {
                                Log.Warning($"[MobaWorldBootstrapModule] Json sink loading failed, falling back to tolerant Resources loading. err={result.Error}");
                                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                            }
                        }
                        else
                        {
                            // Default tolerant loading for Resources
                            db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                        }
                    }

                    return db;
                }
                catch (Exception ex)
                {
                    /* 建库失败属于启动期关键错误，需要可观测（Log）且不能静默吞掉（rethrow）。 */
                    Log.Exception(ex, "[MobaWorldBootstrapModule] MobaConfigDatabase load failed");
                    throw;
                }
            });

            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());
        }

        private static Dictionary<string, byte[]> BuildPartialBytesByKey(
            IMobaConfigBytesSource source,
            string resourcesDir,
            IMobaConfigTableRegistry registry)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (registry == null) return result;

            var tables = registry.Tables;
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

            var tables = registry.Tables;
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
