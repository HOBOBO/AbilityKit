using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Share;
using ShareSyncMode = AbilityKit.Demo.Moba.Share.SyncMode;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    public static class ConsoleConfigLoader
    {
        public const string ConfigDirName = "Configs";
        public const string MobaConfigDir = "luban/moba";

        public static BattleStartConfig LoadBattleStartConfig(ITextAssetLoader? loader = null)
        {
            loader ??= new ConsoleTextAssetLoader();

            var configPath = System.IO.Path.Combine(MobaConfigDir, "battle_start");
            if (loader.TryLoadText(configPath, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleStartConfig>(json);
                    if (config != null)
                    {
                        ApplyEnvironmentOverrides(config);
                        Log.System($"Loaded BattleStartConfig from: {configPath}");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to parse BattleStartConfig from {configPath}: {ex.Message}");
                }
            }

            Log.System("Using default BattleStartConfig");
            var defaultConfig = BattleStartConfig.CreateDefault();
            ApplyEnvironmentOverrides(defaultConfig);
            return defaultConfig;
        }

        /// <summary>
        /// 应用环境变量覆盖配置
        /// 支持以下环境变量：
        /// - SYNC_MODE: Lockstep, SnapshotAuthority, Hybrid
        /// </summary>
        private static void ApplyEnvironmentOverrides(BattleStartConfig config)
        {
            // 检查 SYNC_MODE 环境变量
            var syncMode = Environment.GetEnvironmentVariable("SYNC_MODE");
            if (!string.IsNullOrEmpty(syncMode))
            {
                if (Enum.TryParse<ShareSyncMode>(syncMode, ignoreCase: true, out var mode))
                {
                    config.SyncMode = mode;
                    Log.System($"[ConfigOverride] SyncMode set to: {mode}");
                }
                else
                {
                    Log.Warn($"[ConfigOverride] Unknown SyncMode: {syncMode}, ignoring");
                }
            }
        }

        public static ConsoleMobaConfigDatabase LoadMobaConfig(ITextAssetLoader? loader = null)
        {
            loader ??= new ConsoleTextAssetLoader();
            var db = new ConsoleMobaConfigDatabase(loader);
            db.LoadFromResources(MobaConfigDir);
            return db;
        }

        public static ConsoleMobaConfigDatabase LoadDefault()
        {
            return LoadMobaConfig();
        }
    }
}
