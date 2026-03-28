using AbilityKit.Ability.HotReload;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigLoader
    {
        void Load(MobaConfigDatabase db, IMobaConfigSource source, string resourcesDir = null);
        ConfigReloadResult Reload(MobaConfigDatabase db, IMobaConfigSource source, string resourcesDir = null);

        void LoadFromResources(MobaConfigDatabase db, string resourcesDir);
        ConfigReloadResult ReloadFromResources(MobaConfigDatabase db, string resourcesDir);
    }
}
