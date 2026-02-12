using System;
using System.IO;
using System.Reflection;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Config;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.Share.Common.Reflection;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    internal static class BattleRecordCodecBootstrap
    {
        private const string ModuleKey = "record.lockstep.codec";
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
                if (!ReflectionInvokeUtils.TryInvokeStaticMethod(module.InstallerType, module.GetEffectiveMethod()))
                {
                    Log.Info("[BattleRecordCodecBootstrap] Record codec installer not found/invokable; skip");
                    return false;
                }

                var impl = LockstepInputRecordCodecs.Current != null ? LockstepInputRecordCodecs.Current.GetType().FullName : "<null>";
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
