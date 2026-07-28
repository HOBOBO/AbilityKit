using System;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Presentation.Features.Settlement;

namespace AbilityKit.Game.Flow
{
    internal sealed class UnityMobaFeatureFactoryProvider : IMobaFeatureFactoryProvider
    {
        public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(Func<IBattleSessionFeature> createBattleSessionFeature)
        {
            if (createBattleSessionFeature == null) throw new ArgumentNullException(nameof(createBattleSessionFeature));

            var registry = new MobaFeatureFactoryRegistry()
                .Register("context", (in GamePhaseContext ctx) => new BattleContextFeature())
                .Register("session", (in GamePhaseContext ctx) => createBattleSessionFeature())
                .Register("entity", (in GamePhaseContext ctx) => new BattleEntityFeature())
                .Register("sync", (in GamePhaseContext ctx) => new BattleSyncFeature())
                .Register("input", (in GamePhaseContext ctx) => new BattleInputFeature())
                .Register("view", (in GamePhaseContext ctx) => new BattleViewFeature())
                .Register("hud", (in GamePhaseContext ctx) => new BattleHudFeature())
                .Register("loading_screen", (in GamePhaseContext ctx) => new BattleLoadingScreenFeature())
                .Register("end_recorder", (in GamePhaseContext ctx) => new BattleEndSummaryRecorder())
                .Register("end_settlement", (in GamePhaseContext ctx) => new BattleEndSettlementFeature())
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
