using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterConfig(WorldContainerBuilder builder)
        {
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                _.TryResolve<IMobaConfigDtoDeserializer>(out var deserializer);
                _.TryResolve<IMobaConfigDtoBytesDeserializer>(out var bytesDeserializer);
                var db = new MobaConfigDatabase(registry, deserializer, bytesDeserializer);
                var loader = _.TryResolve<IMobaConfigLoader>(out var injected) && injected != null
                    ? injected
                    : new DefaultMobaConfigLoader(registry ?? DefaultMobaConfigTableRegistry.Instance);

                var bytesLoader = _.TryResolve<IMobaConfigBytesLoader>(out var injectedBytesLoader) && injectedBytesLoader != null
                    ? injectedBytesLoader
                    : new DefaultMobaConfigBytesLoader(registry ?? DefaultMobaConfigTableRegistry.Instance);
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

                    if (format == MobaConfigFormat.Bytes)
                    {
                        if (_.TryResolve<IMobaConfigBytesSource>(out var bsource) && bsource != null)
                        {
                            bytesLoader.Load(db, bsource, MobaConfigPaths.DefaultResourcesBytesDir);
                        }
                        else
                        {
                            bytesLoader.LoadFromResources(db, MobaConfigPaths.DefaultResourcesBytesDir);
                        }
                    }
                    else
                    {
                        if (_.TryResolve<IMobaConfigSource>(out var source) && source != null)
                        {
                            loader.Load(db, source, MobaConfigPaths.DefaultResourcesDir);
                        }
                        else if (_.TryResolve<IMobaConfigTextSink>(out var sink) && sink != null)
                        {
                            loader.Load(db, new MobaConfigTextSinkAdapter(sink), MobaConfigPaths.DefaultResourcesDir);
                        }
                        else
                        {
                            loader.LoadFromResources(db, MobaConfigPaths.DefaultResourcesDir);
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
    }
}
