using AbilityKit.Ability.Share.Common.Log;
#if UNITY_EDITOR
using UnityEngine;
#endif
namespace AbilityKit.Ability.HotReload
{
    internal static class ConfigReloadDebugListener
    {
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        private static void Init()
        {
            ConfigReloadBus.Reloaded -= OnReloaded;
            ConfigReloadBus.Reloaded += OnReloaded;
        }

        private static void OnReloaded(ConfigReloadResult r)
        {
            if (r.Succeeded)
            {
                Log.Info($"[ConfigReload] ok key={r.Key} version={r.Version} full={r.FullReload} changed={(r.ChangedIds != null ? r.ChangedIds.Count : 0)}");
            }
            else
            {
                Log.Error($"[ConfigReload] fail key={r.Key} version={r.Version} error={r.Error}");
            }
        }
    }
}
