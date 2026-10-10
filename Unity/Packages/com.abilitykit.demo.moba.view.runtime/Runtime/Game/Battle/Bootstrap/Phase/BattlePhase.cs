using System;

namespace AbilityKit.Game.Flow
{
    public sealed class BattlePhase : IGamePhase
    {
        private readonly IBattleBootstrapper _bootstrapper;
        private readonly Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> _gatewayConnectionFactory;

        public BattlePhase(IBattleBootstrapper bootstrapper, Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> gatewayConnectionFactory = null)
        {
            _bootstrapper = bootstrapper;
            _gatewayConnectionFactory = gatewayConnectionFactory;
        }

        public IBattleBootstrapper Bootstrapper => _bootstrapper;
        public Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> GatewayConnectionFactory =>
            _gatewayConnectionFactory;

        public void Enter(in GamePhaseContext ctx)
        {
            var flow = ctx.Entry.Get<GameFlowDomain>();

            flow.EnterBattle(_bootstrapper, _gatewayConnectionFactory);
        }

        public void Exit(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
