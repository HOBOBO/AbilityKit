using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        BattleStartPlan ISessionOrchestratorHost.Plan => _plan;
        BattleContext ISessionOrchestratorHost.Context => _ctx;
        Action<FramePacket> ISessionOrchestratorHost.FrameReceivedHandler => OnFrame;
        BattleLogicSession ISessionOrchestratorHost.StartBattleLogicSession(BattleLogicSessionOptions opts) => StartBattleLogicSession(opts);
        void ISessionOrchestratorHost.InvokeSessionStartingPipeline() => InvokeSessionStartingPipeline();
        void ISessionOrchestratorHost.InvokeSessionStoppingPipeline() => InvokeSessionStoppingPipeline();
        void ISessionOrchestratorHost.InvokeReplaySetupPipeline() => InvokeReplaySetupPipeline();
        void ISessionOrchestratorHost.StartRemoteDrivenLocalWorld() => StartRemoteDrivenLocalWorld();
        void ISessionOrchestratorHost.StartConfirmedAuthorityWorld() => StartConfirmedAuthorityWorld();
        void ISessionOrchestratorHost.TryDestroyBattleWorlds() => TryDestroyBattleWorlds();
        void ISessionOrchestratorHost.DisposeSnapshotRouting() => DisposeSnapshotRouting();
        void ISessionOrchestratorHost.DisposeConfirmedView() => DisposeConfirmedView();
        void ISessionOrchestratorHost.DisposeRemoteDrivenWorld() => DisposeRemoteDrivenWorld();
        void ISessionOrchestratorHost.DisposeConfirmedWorld() => DisposeConfirmedWorld();
        void ISessionOrchestratorHost.DisposeNetworkIoDispatcher() => DisposeNetworkIoDispatcher();
        void ISessionOrchestratorHost.ResetHandles() => ResetHandles();
    }
}
