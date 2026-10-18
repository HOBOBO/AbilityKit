using System;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Battle.Presentation.Features.Scene;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Flow
{
    internal static class MobaBattleFeatureRegistration
    {
        internal static void Register(MobaFeatureFactoryRegistry registry,
            Func<IBattleSessionFeature> createSession,
            GameplayPresentationProfileCatalog profiles,
            GameplayPresentationModuleRegistry modules,
            IBattleAssetLoadService assetLoadService,
            IBattleAssetDependencyProvider assetDependencyProvider,
            MobaDungeonRegistry dungeonRegistry = null)
        {
            var contextPorts = new[]
            {
                typeof(BattleContext), typeof(IBattleRuntimeContext), typeof(IBattleEntityContext),
                typeof(IBattleInputContext), typeof(IBattleSnapshotRoutingContext),
                typeof(IBattleEntityRuntimeBindingPort),
                typeof(IBattleHudInputSink), typeof(IBattleGameplayPresentationSource),
                typeof(IBattleHudPresentationPort)
            };
            var contextRequirement = new[] { typeof(BattleContext) };

            registry
                .Register("context", (in GamePhaseContext ctx) => new BattleContextFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle, providedPorts: contextPorts))
                .Register("entity", (in GamePhaseContext ctx) => new BattleEntityFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context" }, new[] { typeof(IBattleEntityRuntimeBindingPort) }))
                .Register("session", (in GamePhaseContext ctx) => createSession(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context", "entity" }, contextRequirement,
                        new[] { typeof(IBattleAssetLoadSessionPort) }))
                .Register("battle_outcome", (in GamePhaseContext ctx) => new BattleOutcomeFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "session" }, new[] { typeof(IBattleGameplayPresentationSource) }))
                .Register("dungeon_scene", (in GamePhaseContext ctx) => new BattleSceneFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context" }, null, null))
                .Register("dungeon_context", (in GamePhaseContext ctx) => new BattleDungeonFeature(),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "context" }, null,
                        new[] { typeof(MobaDungeonDef), typeof(MobaDungeonRegistry),
                                typeof(IDungeonGameplayHandler) }))
                .Register("gameplay_presentation", (in GamePhaseContext ctx) =>
                        new BattleGameplayPresentationFeature(profiles, modules),
                    new MobaFeatureContract(MobaFeatureLifetime.Battle,
                        new[] { "session" }, new[] { typeof(IBattleGameplayPresentationSource) }))
                .Register("sync", (in GamePhaseContext ctx) => new BattleSyncFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "session" }, requiredPorts: contextRequirement))
                .Register("input", (in GamePhaseContext ctx) => new BattleInputFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync" }, requiredPorts: contextRequirement))
                .Register("view", (in GamePhaseContext ctx) => new BattleViewFeature(),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync" }, requiredPorts: contextRequirement))
                .Register("hud", (in GamePhaseContext ctx) => new BattleHudFeature(modules),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "sync", "gameplay_presentation" },
                        requiredPorts: contextRequirement,
                        satisfiedPortGroups: new[] { "deathmatch" }))
                .Register("loading_screen", (in GamePhaseContext ctx) =>
                        new BattleLoadingScreenFeature(assetLoadService, assetDependencyProvider),
                    new MobaFeatureContract(requiredFeatureIds: new[] { "session" },
                        requiredPorts: new[] { typeof(IBattleAssetLoadSessionPort) }))
                .Register("end_settlement", (in GamePhaseContext ctx) =>
                {
                    var moduleId = ctx.GameplayPresentation?.SettlementModuleId;
                    if (string.IsNullOrWhiteSpace(moduleId))
                        throw new InvalidOperationException("Battle presentation modules were not selected before settlement.");
                    return modules.CreateSettlement(moduleId);
                });
        }
    }
}
