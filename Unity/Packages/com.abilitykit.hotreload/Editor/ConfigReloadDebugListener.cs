using AbilityKit.Core.Logging;
using UnityEngine;

namespace AbilityKit.Ability.HotReload
{
    /// <summary>
    /// 在编辑器播放时记录配置重载结果，不参与配置应用或热替换。
    /// </summary>
    internal static class ConfigReloadDebugListener
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            ConfigReloadBus.Reloaded -= OnReloaded;
            ConfigReloadBus.Reloaded += OnReloaded;
        }

        private static void OnReloaded(ConfigReloadResult result)
        {
            if (result.Succeeded)
            {
                Log.Info($"[ConfigReload] ok key={result.Key} version={result.Version} full={result.FullReload} changed={result.ChangedIds?.Count ?? 0}");
            }
            else
            {
                Log.Error($"[ConfigReload] fail key={result.Key} version={result.Version} error={result.Error}");
            }
        }
    }
}
