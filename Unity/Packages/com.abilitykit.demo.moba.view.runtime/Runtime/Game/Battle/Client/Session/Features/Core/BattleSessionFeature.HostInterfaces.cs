using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal interface ITickLoopHost
    {
        float GetFixedDeltaSeconds();

        void TickRemoteDrivenLocalSim(float deltaTime);
        void TickConfirmedAuthorityWorldSim(float deltaTime);
        void TickRemoteInterpolation(float deltaTime);
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

        void TryDestroyBattleWorlds();
        void DisposeSnapshotRouting();
        void DisposeConfirmedView();
        void DisposeRemoteDrivenWorld();
        void DisposeConfirmedWorld();
        void DisposeRemoteInterpolation();

        void ResetSessionHandles();
    }
}
