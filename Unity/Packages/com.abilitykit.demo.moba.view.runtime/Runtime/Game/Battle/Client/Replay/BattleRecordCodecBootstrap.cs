using System;
using System.IO;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Bootstrap;
using AbilityKit.Core.Recording.FrameRecord;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    internal static class BattleRecordCodecBootstrap
    {
        private const string ModuleKey = "record.framerecord.codec";
        private const string ConfigFileName = "abilitykit.features.json";
        private static bool s_tried;
        private static bool s_installed;

        internal static bool TryInstallMemoryPack()
        {
            if (s_tried) return s_installed;
            s_tried = true;

            var path = ResolveConfigPath(ConfigFileName);
            var cfg = PersistentJsonConfigLoader.LoadOrDefault<ModuleInstallerConfigSet>(path, JsonUtility.FromJson<ModuleInstallerConfigSet>);
            var module = cfg != null ? cfg.FindModule(ModuleKey) : null;
            if (module == null) return false;

            try
            {
                if (!ModuleInstallerInvoker.TryInvoke(module))
                {
                    Log.Info("[BattleRecordCodecBootstrap] Record codec installer not found/invokable; skip");
                    return false;
                }

                var impl = FrameRecordCodecs.Current != null ? FrameRecordCodecs.Current.GetType().FullName : "<null>";
                Log.Info($"[BattleRecordCodecBootstrap] MemoryPack record codec installed. current={impl}");
                s_installed = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleRecordCodecBootstrap] Install MemoryPack record codec failed");
                return false;
            }
        }

        private static string ResolveConfigPath(string fileName)
        {
            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            if (string.IsNullOrEmpty(baseDir)) return fileName;
            return Path.Combine(baseDir, fileName);
        }
    }
}
