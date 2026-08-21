using System;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal interface ITickLoopHost
    {
        float GetFixedDeltaSeconds();

        void TickRemoteDrivenLocalSim(float deltaTime);
        void TickConfirmedAuthorityWorldSim(float deltaTime);
        void TickRemoteInterpolation(float deltaTime);
    }

    internal interface ISessionLogicPort
    {
        BattleStartPlan Plan { get; }
        BattleContext Context { get; }
        Action<FramePacket> FrameReceivedHandler { get; }

        BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions options);
        void StopBattleLogicSession();
    }

    internal interface ISessionPipelinePort
    {
        void InvokeSessionStartingPipeline();
        void InvokeSessionStoppingPipeline();
        void InvokeReplaySetupPipeline();
    }

    internal interface ISessionRuntimeResourcesPort
    {
        void StartRemoteDrivenLocalWorld();
        void StartConfirmedAuthorityWorld();
        void DisposeReplayRecordWriter();
        Task StopRecoveryAsync();
        void TryDestroyBattleWorlds();
        void DisposeSnapshotRouting();
        void DisposeConfirmedView();
        void DisposeRemoteDrivenWorld();
        void DisposeConfirmedWorld();
        void DisposeRemoteInterpolation();
        void ResetSessionHandles();
    }

    internal interface ISessionOrchestratorHost
    {
        BattleStartPlan Plan { get; }
        BattleContext Context { get; }

        void StartBattleLogicSession(BattleLogicSessionOptions opts);
        void SubscribeFrameReceived();
        void UnsubscribeFrameReceived();
        void StopBattleLogicSession();

        void InvokeSessionStartingPipeline();
        void InvokeSessionStoppingPipeline();
        void InvokeReplaySetupPipeline();

        void StartRemoteDrivenLocalWorld();
        void StartConfirmedAuthorityWorld();

        void DisposeReplayRecordWriter();
        Task StopRecoveryAsync();
        void TryDestroyBattleWorlds();
        void DisposeSnapshotRouting();
        void DisposeConfirmedView();
        void DisposeRemoteDrivenWorld();
        void DisposeConfirmedWorld();
        void DisposeRemoteInterpolation();

        void ResetSessionHandles();
    }
}
