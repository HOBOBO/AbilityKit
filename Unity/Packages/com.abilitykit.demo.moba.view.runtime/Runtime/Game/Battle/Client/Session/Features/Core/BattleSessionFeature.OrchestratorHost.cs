using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    // Keeps SessionOrchestrator dependent on lifecycle capabilities instead of feature delegates.
    internal sealed class SessionLifecycleHost : ISessionOrchestratorHost
    {
        private readonly BattleSessionHandles _handles;
        private readonly ISessionLogicPort _logic;
        private readonly ISessionPipelinePort _pipeline;
        private readonly ISessionRuntimeResourcesPort _resources;

        public SessionLifecycleHost(
            BattleSessionHandles handles,
            ISessionLogicPort logic,
            ISessionPipelinePort pipeline,
            ISessionRuntimeResourcesPort resources)
        {
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public BattleStartPlan Plan => _logic.Plan;
        public BattleContext Context => _logic.Context;

        public void StartBattleLogicSession(BattleLogicSessionOptions options)
        {
            _handles.Session = _logic.StartBattleLogicSession(options)
                ?? throw new InvalidOperationException("Battle logic session port returned null.");
        }

        public void SubscribeFrameReceived()
        {
            var session = _handles.Session
                ?? throw new InvalidOperationException("Cannot subscribe before the battle logic session is available.");
            session.FrameReceived += _logic.FrameReceivedHandler;
        }

        public void UnsubscribeFrameReceived()
        {
            var session = _handles.Session;
            if (session != null) session.FrameReceived -= _logic.FrameReceivedHandler;
        }

        public void StopBattleLogicSession() => _logic.StopBattleLogicSession();
        public void InvokeSessionStartingPipeline() => _pipeline.InvokeSessionStartingPipeline();
        public void InvokeSessionStoppingPipeline() => _pipeline.InvokeSessionStoppingPipeline();
        public void InvokeReplaySetupPipeline() => _pipeline.InvokeReplaySetupPipeline();
        public void StartRemoteDrivenLocalWorld() => _resources.StartRemoteDrivenLocalWorld();
        public void StartConfirmedAuthorityWorld() => _resources.StartConfirmedAuthorityWorld();
        public void DisposeReplayRecordWriter() => _resources.DisposeReplayRecordWriter();
        public void TryDestroyBattleWorlds() => _resources.TryDestroyBattleWorlds();
        public void DisposeSnapshotRouting() => _resources.DisposeSnapshotRouting();
        public void DisposeConfirmedView() => _resources.DisposeConfirmedView();
        public void DisposeRemoteDrivenWorld() => _resources.DisposeRemoteDrivenWorld();
        public void DisposeConfirmedWorld() => _resources.DisposeConfirmedWorld();
        public void DisposeRemoteInterpolation() => _resources.DisposeRemoteInterpolation();
        public void ResetSessionHandles() => _resources.ResetSessionHandles();
    }

    public sealed partial class BattleSessionFeature
    {
        BattleStartPlan ISessionLogicPort.Plan => _plan;
        BattleContext ISessionLogicPort.Context => _ctx;
        Action<FramePacket> ISessionLogicPort.FrameReceivedHandler => OnFrame;

        BattleLogicSession ISessionLogicPort.StartBattleLogicSession(BattleLogicSessionOptions options) =>
            StartBattleLogicSession(options);

        void ISessionLogicPort.StopBattleLogicSession() => _sessionRegistry.Stop();
        void ISessionPipelinePort.InvokeSessionStartingPipeline() => InvokeSessionStartingPipeline();
        void ISessionPipelinePort.InvokeSessionStoppingPipeline() => InvokeSessionStoppingPipeline();
        void ISessionPipelinePort.InvokeReplaySetupPipeline() => InvokeReplaySetupPipeline();
        void ISessionRuntimeResourcesPort.StartRemoteDrivenLocalWorld() => StartRemoteDrivenLocalWorld();
        void ISessionRuntimeResourcesPort.StartConfirmedAuthorityWorld() => StartConfirmedAuthorityWorld();
        void ISessionRuntimeResourcesPort.DisposeReplayRecordWriter() => _runtime.Replay.DisposeRecordWriter();
        void ISessionRuntimeResourcesPort.TryDestroyBattleWorlds() => TryDestroyBattleWorlds();
        void ISessionRuntimeResourcesPort.DisposeSnapshotRouting() => DisposeSnapshotRouting();
        void ISessionRuntimeResourcesPort.DisposeConfirmedView() => DisposeConfirmedView();
        void ISessionRuntimeResourcesPort.DisposeRemoteDrivenWorld() => DisposeRemoteDrivenWorld();
        void ISessionRuntimeResourcesPort.DisposeConfirmedWorld() => DisposeConfirmedWorld();
        void ISessionRuntimeResourcesPort.DisposeRemoteInterpolation() => DisposeRemoteInterpolation();
        void ISessionRuntimeResourcesPort.ResetSessionHandles() => ResetSessionHandles();
    }
}
