namespace AbilityKit.Game.Flow
{
    internal static class MobaLobbyFeatureRegistration
    {
        internal static void Register(MobaFeatureFactoryRegistry registry, MobaLobbyRuntimeOptions options)
        {
            registry
                .Register("demo_lobby", (in GamePhaseContext ctx) => new DemoLobbyOnGUIFeature())
                .Register("formal_lobby", (in GamePhaseContext ctx) => new FormalLobbyFeature(options))
                .Register("formal_lobby_ui", (in GamePhaseContext ctx) => new FormalLobbyOnGUIFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "formal_lobby" }));
        }
    }
}
