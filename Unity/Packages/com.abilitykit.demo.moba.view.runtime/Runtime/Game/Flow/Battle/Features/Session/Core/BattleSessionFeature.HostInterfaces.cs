using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        internal interface ITickLoopHost
        {
            float GetFixedDeltaSeconds();

            void TickRemoteDrivenLocalSim(float deltaTime);
            void TickConfirmedAuthorityWorldSim(float deltaTime);
        }

        internal interface ISessionOrchestratorHost
        {
            BattleStartPlan Plan { get; }
            BattleContext Context { get; }

            Action<FramePacket> FrameReceivedHandler { get; }

            BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions opts);

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
            void DisposeNetworkIoDispatcher();

            void ResetHandles();
        }
    }
}
