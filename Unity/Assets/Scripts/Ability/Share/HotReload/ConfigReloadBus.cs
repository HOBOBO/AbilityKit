using System;

namespace AbilityKit.Ability.HotReload
{
    public static class ConfigReloadBus
    {
        public static event Action<ConfigReloadResult> Reloaded;

        public static void Publish(ConfigReloadResult result)
        {
            var handler = Reloaded;
            handler?.Invoke(result);
        }
    }
}
