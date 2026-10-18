namespace AbilityKit.Game.Flow
{
    internal static class MobaUiFeatureRegistration
    {
        internal static void Register(MobaFeatureFactoryRegistry registry)
        {
            registry
                .Register("root_debug", (in GamePhaseContext ctx) => new RootDebugOnGUIFeature())
                .Register("debug_ongui", (in GamePhaseContext ctx) => new BattleDebugOnGUIFeature());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            registry.Register("boot_menu", (in GamePhaseContext ctx) => new BootMenuOnGUIFeature());
#endif
        }
    }
}
