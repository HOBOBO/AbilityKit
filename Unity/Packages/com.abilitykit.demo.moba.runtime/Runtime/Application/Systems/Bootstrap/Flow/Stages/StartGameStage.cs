using System;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    [MobaBootstrapStage]
    public sealed class StartGameStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.StartGame;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.MapRuntime,
        };

        protected internal override void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
            if (!services.TryResolve<IMobaPendingGameStartSpecStore>(out var specs) || specs == null || !specs.TryGetPlan(out var plan))
            {
                throw new InvalidOperationException("StartGameStage requires a validated MobaBattleStartPlan produced by WorldInitStage.");
            }

            var planValidation = specs.ValidatePendingPlan();
            if (!planValidation.Succeeded)
            {
                throw new InvalidOperationException("StartGameStage battle start plan validation failed. " + planValidation.Message);
            }

            var spec = plan.ToGameStartSpec();

            if (!services.TryResolve<IMobaGameStartPort>(out var gameStart) || gameStart == null)
            {
                throw new InvalidOperationException("StartGameStage requires IMobaGameStartPort to start the battle.");
            }

            var result = gameStart.TryStartGame(in spec);
            if (result.Succeeded)
            {
                specs.Clear();
                Log.Info("[StartGameStage] battle start plan applied");
                return;
            }

            throw new InvalidOperationException($"StartGameStage game start spec rejected. {result}");
        }
    }
}
