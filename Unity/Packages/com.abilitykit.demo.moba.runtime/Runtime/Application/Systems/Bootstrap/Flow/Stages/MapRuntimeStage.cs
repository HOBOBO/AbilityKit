using System;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Services.Navigation;

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
            if (!services.TryResolve<IMobaMapRuntimeService>(out var maps) || maps == null)
            {
                throw new InvalidOperationException("MapRuntimeStage requires IMobaMapRuntimeService.");
            }

            maps.Load(mapId);

            if (services.TryResolve<MobaNavigationService>(out var navigation) && navigation != null)
            {
                navigation.Build();
                Log.Info($"[MapRuntimeStage] navigation grid baked. mapId={mapId}, built={navigation.IsBuilt}");
            }

            Log.Info($"[MapRuntimeStage] battle map loaded. mapId={mapId}, name={maps.CurrentMap?.Name}");
        }
    }
}
