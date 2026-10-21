using System;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Services.Navigation;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Gameplay;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    [MobaBootstrapStage]
    public sealed class MapRuntimeStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.MapRuntime;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.WorldInit,
        };

        protected internal override void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
            if (!services.TryResolve<IMobaPendingGameStartSpecStore>(out var specs) || specs == null || !specs.TryGet(out var spec))
            {
                throw new InvalidOperationException("MapRuntimeStage requires a validated MobaGameStartSpec produced by WorldInitStage.");
            }

            int mapId = spec.EnterReq.MapId;
            int gameplayId = spec.EnterReq.GameplayId;
            if (!services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null)
            {
                throw new InvalidOperationException("MapRuntimeStage requires MobaConfigDatabase.");
            }

            if (!configs.TryGetGameplay(gameplayId, out GameplayMO gameplay) || gameplay == null)
            {
                throw new InvalidOperationException($"Gameplay config was not found. gameplayId={gameplayId}");
            }

            if (gameplay.MapId <= 0 || gameplay.MapId != mapId)
            {
                throw new InvalidOperationException(
                    $"Gameplay map does not match create-world map. gameplayId={gameplayId}, gameplayMapId={gameplay.MapId}, requestedMapId={mapId}");
            }

            if (!services.TryResolve<IMobaMapRuntimeService>(out var maps) || maps == null)
            {
                throw new InvalidOperationException("MapRuntimeStage requires IMobaMapRuntimeService.");
            }

            maps.Load(mapId);

            if (!services.TryResolve<IMobaGameplayFeatureSet>(out var features) || features == null)
                throw new InvalidOperationException("MapRuntimeStage requires IMobaGameplayFeatureSet.");

            features.Reset();
            var featureIds = gameplay.FeatureIds ?? Array.Empty<string>();
            for (var i = 0; i < featureIds.Count; i++)
                features.Enable(featureIds[i]);

            if (features.IsEnabled(MobaGameplayFeatureIds.Structures))
            {
                if (!services.TryResolve<IMobaMapStructureRuntimeConfig>(out var structures) || structures == null)
                    throw new InvalidOperationException(
                        $"Gameplay feature '{MobaGameplayFeatureIds.Structures}' requires IMobaMapStructureRuntimeConfig. gameplayId={gameplayId}");
                structures.Prepare(maps.CurrentMap);
                if (!services.TryResolve<MobaStructureService>(out var structureService) || structureService == null)
                    throw new InvalidOperationException("Structure gameplay feature requires MobaStructureService.");
                structureService.Prepare(structures.Resource);
                Log.Info($"[MapRuntimeStage] optional structure feature enabled. mapId={mapId}, gameplayId={gameplayId}");
            }

            if (features.IsEnabled(MobaGameplayFeatureIds.MinionWaves))
            {
                if (!services.TryResolve<IMobaMinionRouteRuntimeConfig>(out var minionRoutes) || minionRoutes == null)
                    throw new InvalidOperationException(
                        $"Gameplay feature '{MobaGameplayFeatureIds.MinionWaves}' requires IMobaMinionRouteRuntimeConfig. gameplayId={gameplayId}");

                minionRoutes.Prepare(maps.CurrentMap);
                Log.Info($"[MapRuntimeStage] optional minion wave feature enabled. mapId={mapId}, gameplayId={gameplayId}");
            }

            if (services.TryResolve<MobaNavigationService>(out var navigation) && navigation != null)
            {
                navigation.Build();
                Log.Info($"[MapRuntimeStage] navigation grid baked. mapId={mapId}, built={navigation.IsBuilt}");
            }

            Log.Info($"[MapRuntimeStage] battle map loaded. mapId={mapId}, name={maps.CurrentMap?.Name}");
        }
    }
}
