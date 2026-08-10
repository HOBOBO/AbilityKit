using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the mutable state and runtime resources of one battle session.
    /// The feature remains a compatibility facade during the staged migration.
    /// </summary>
    internal sealed class BattleSessionRuntime
    {
        internal BattleSessionState State { get; }
        internal BattleSessionHandles Handles { get; }
        internal SessionOrchestrator Orchestrator { get; private set; }
        internal BattleSnapshotRoutingRuntime SnapshotRouting { get; }
        internal GatewaySessionRuntime GatewayRoom { get; private set; }

        internal BattleSessionRuntime()
        {
            State = new BattleSessionState();
            Handles = new BattleSessionHandles();
            SnapshotRouting = new BattleSnapshotRoutingRuntime(Handles);
        }

        internal void ConfigureGatewayRoom(
            IAbilityKitConnectionRegistry connectionRegistry,
            IBattleSessionGatewayConnectionFactory connectionFactory,
            IBattleSessionGatewayRoomClientFactory clientFactory,
            NetworkConditionController networkCondition)
        {
            if (GatewayRoom != null)
            {
                throw new InvalidOperationException("Battle session gateway room has already been configured.");
            }

            GatewayRoom = new GatewaySessionRuntime(
                Handles,
                connectionRegistry,
                connectionFactory,
                clientFactory,
                networkCondition);
        }

        internal void ConfigureOrchestrator(ISessionOrchestratorHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (Orchestrator != null)
            {
                throw new InvalidOperationException("Battle session orchestrator has already been configured.");
            }

            Orchestrator = new SessionOrchestrator(State, Handles, host);
        }
    }
}
