#nullable enable

using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public static class ShooterGameplayScenarioWorldHostFactory
    {
        public static ShooterBattleWorldSession CreateBattleWorld(
            string? worldId,
            ShooterPlayModeSessionOptions sessionOptions)
        {
            return ShooterBattleWorldSession.Create(
                worldId,
                CreateClient(sessionOptions));
        }

        public static ShooterWorldHost CreateClient(ShooterPlayModeSessionOptions sessionOptions)
        {
            return new ShooterWorldHost(options =>
            {
                ConfigureWorldOptions(options, sessionOptions.GameplayScenario);
                if (UsesAuthoritativeRemoteWorld(sessionOptions.SyncModel))
                {
                    options.Extensions[typeof(ShooterEnemySimulationOverride)] =
                        new ShooterEnemySimulationOverride(enabled: false);
                }
            });
        }

        public static ShooterWorldHost Create(ShooterSveltoGameplayScenarioConfig? scenario)
        {
            return new ShooterWorldHost(options => ConfigureWorldOptions(options, scenario));
        }

        public static void ConfigureWorldOptions(
            WorldCreateOptions options,
            ShooterSveltoGameplayScenarioConfig? scenario)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            // RVO 加速走 ShooterWorldModule 默认注册的共享并行服务（排序网格 + Parallel.For
            // 邻居收集 + 并行 ORCA）。2026-08-26 编辑器 2048 单位实测：该组合邻居收集
            // 3.8ms/帧，优于 Burst jobs hashmap 收集的 10.8ms/帧（拷贝与预扫描税），
            // 因此不再默认挂 ShooterUnityJobsWorldModule；需要 jobs 收集时显式组合。
            if (scenario.HasValue)
            {
                options.Extensions[typeof(ShooterSveltoGameplayScenarioConfig)] = scenario.Value;
            }
        }

        private static bool UsesAuthoritativeRemoteWorld(NetworkSyncModel syncModel)
        {
            return syncModel == NetworkSyncModel.AuthoritativeInterpolation
                || syncModel == NetworkSyncModel.BatchStateSync
                || syncModel == NetworkSyncModel.MassBattleLodSync;
        }

        public static void ConfigureWorldOptions(
            WorldCreateOptions options,
            in ShooterSveltoGameplayScenarioConfig scenario)
        {
            ConfigureWorldOptions(options, (ShooterSveltoGameplayScenarioConfig?)scenario);
        }
    }
}
