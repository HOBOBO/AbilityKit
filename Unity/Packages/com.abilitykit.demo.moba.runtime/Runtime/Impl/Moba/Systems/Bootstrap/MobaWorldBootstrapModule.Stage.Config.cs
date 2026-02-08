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
            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                var db = new MobaConfigDatabase();
                try
                {
                    /*
                     * 职责边界/数据流：
                     * - MobaConfigDatabase 是逻辑层“读表后的配置数据库”，供技能/BUFF/召唤/初始化等系统查询。
                     * - 接入方只需提供 IMobaConfigTextSink（表数据读取 sink），逻辑层负责解析与建库。
                     * - 若未提供 sink，则 fallback 到 Unity Resources（保持旧行为）。
                     */
                    if (_.TryResolve<IMobaConfigTextSink>(out var sink) && sink != null)
                    {
                        db.LoadFromTextSink(sink, MobaConfigPaths.DefaultResourcesDir);
                    }
                    else
                    {
                        db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
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
