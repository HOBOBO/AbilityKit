using System;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Presentation.Features.Settlement;

namespace AbilityKit.Game.Flow
{
    internal sealed class UnityMobaFeatureFactoryProvider : IMobaFeatureFactoryProvider
    {
        private readonly GameplayPresentationProfileCatalog _profiles;
        private readonly GameplayPresentationModuleRegistry _modules;

        internal UnityMobaFeatureFactoryProvider(
            GameplayPresentationProfileCatalog profiles = null,
            GameplayPresentationModuleRegistry modules = null)
        {
            _profiles = profiles ?? GameplayPresentationProfileCatalog.Default;
            _modules = modules ?? GameplayPresentationModuleRegistry.CreateStandard();
        }

        public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(Func<IBattleSessionFeature> createBattleSessionFeature)
        {
            if (createBattleSessionFeature == null) throw new ArgumentNullException(nameof(createBattleSessionFeature));
            _profiles.ValidateModules(_modules);

            var battleContext = new[]
            {
                typeof(BattleContext), typeof(IBattleRuntimeContext), typeof(IBattleEntityContext),
                typeof(IBattleInputContext), typeof(IBattleSnapshotRoutingContext),
                typeof(IBattleHudInputSink), typeof(IBattleGameplayPresentationSource),
                typeof(IBattleHudPresentationPort)
            };
            var registry = new MobaFeatureFactoryRegistry()
                .Register("context", (in GamePhaseContext ctx) => new BattleContextFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle, providedPorts: battleContext))
                .Register("session", (in GamePhaseContext ctx) => createBattleSessionFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context", "entity" }, battleContext,
                        new[] { typeof(IBattleAssetLoadSessionPort) }))
                .Register("gameplay_presentation", (in GamePhaseContext ctx) => new BattleGameplayPresentationFeature(_profiles, _modules),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "session" }, new[] { typeof(IBattleGameplayPresentationSource) }))
                .Register("battle_outcome", (in GamePhaseContext ctx) => new BattleOutcomeFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "session" }, new[] { typeof(IBattleGameplayPresentationSource) }))
                .Register("entity", (in GamePhaseContext ctx) => new BattleEntityFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context" }, battleContext))
                .Register("sync", (in GamePhaseContext ctx) => new BattleSyncFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "session" }, requiredPorts: battleContext))
                .Register("input", (in GamePhaseContext ctx) => new BattleInputFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync" }, requiredPorts: battleContext))
                .Register("view", (in GamePhaseContext ctx) => new BattleViewFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync" }, requiredPorts: battleContext))
                .Register("hud", (in GamePhaseContext ctx) => new BattleHudFeature(_modules),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync" }, requiredPorts: battleContext))
                .Register("loading_screen", (in GamePhaseContext ctx) => new BattleLoadingScreenFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "session" },
                        requiredPorts: new[] { typeof(IBattleAssetLoadSessionPort) }))
                .Register("end_settlement", (in GamePhaseContext ctx) =>
                {
                    var moduleId = ctx.GameplayPresentation?.SettlementModuleId;
                    if (string.IsNullOrWhiteSpace(moduleId))
                        throw new InvalidOperationException("Battle presentation modules were not selected before settlement.");
                    return _modules.CreateSettlement(moduleId);
                })
                .Register("demo_lobby", (in GamePhaseContext ctx) => new DemoLobbyOnGUIFeature())
                .Register("formal_lobby", (in GamePhaseContext ctx) => new FormalLobbyFeature())
                .Register("root_debug", (in GamePhaseContext ctx) => new RootDebugOnGUIFeature())
                .Register("debug_ongui", (in GamePhaseContext ctx) => new BattleDebugOnGUIFeature());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            registry.Register("boot_menu", (in GamePhaseContext ctx) => new BootMenuOnGUIFeature());
#endif

            return registry;
        }
    }
}
