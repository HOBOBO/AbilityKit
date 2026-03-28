using AbilityKit.Ability.HotReload;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigBytesLoader
    {
        void Load(MobaConfigDatabase db, IMobaConfigBytesSource source, string resourcesDir = null);
        ConfigReloadResult Reload(MobaConfigDatabase db, IMobaConfigBytesSource source, string resourcesDir = null);

        void LoadFromResources(MobaConfigDatabase db, string resourcesDir);
        ConfigReloadResult ReloadFromResources(MobaConfigDatabase db, string resourcesDir);
    }
}
