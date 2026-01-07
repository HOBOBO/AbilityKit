using System;

namespace AbilityKit.Game.Flow
{
    public sealed class BattlePhase : IGamePhase
    {
        private readonly IBattleBootstrapper _bootstrapper;

        public BattlePhase(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        public void Enter(in GamePhaseContext ctx)
        {
            var flow = ctx.Entry.Get<GameFlowDomain>();

            flow.Attach(new BattleSessionFeature(_bootstrapper));
            flow.Attach(new BattleInputFeature());
            flow.Attach(new BattleViewFeature());
            flow.Attach(new BattleDebugOnGUIFeature());
        }

        public void Exit(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
