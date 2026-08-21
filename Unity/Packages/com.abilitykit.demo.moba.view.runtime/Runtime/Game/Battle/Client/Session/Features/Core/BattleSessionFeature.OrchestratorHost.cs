using System;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.World.ECS;

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
        public Task StopRecoveryAsync() => _resources.StopRecoveryAsync();
        public void TryDestroyBattleWorlds() => _resources.TryDestroyBattleWorlds();
        public void DisposeSnapshotRouting() => _resources.DisposeSnapshotRouting();
        public void DisposeConfirmedView() => _resources.DisposeConfirmedView();
        public void DisposeRemoteDrivenWorld() => _resources.DisposeRemoteDrivenWorld();
        public void DisposeConfirmedWorld() => _resources.DisposeConfirmedWorld();
        public void DisposeRemoteInterpolation() => _resources.DisposeRemoteInterpolation();
        public void ResetSessionHandles() => _resources.ResetSessionHandles();
    }

    internal sealed class SessionRuntimeResourcesPort : ISessionRuntimeResourcesPort
    {
        private readonly BattleSessionRuntime _runtime;
        private readonly Func<BattleStartPlan> _getPlan;
        private readonly Func<BattleContext> _getContext;
        private readonly Func<GameFlowDomain> _getFlow;
        private readonly Func<bool> _hasLogicSession;
        private readonly Func<float> _getFixedDeltaSeconds;
        private readonly Func<WorldId, int> _resolveIdealFrameLimit;
        private readonly Action<IEntity> _destroyEntityTree;

        internal SessionRuntimeResourcesPort(
            BattleSessionRuntime runtime,
            Func<BattleStartPlan> getPlan,
            Func<BattleContext> getContext,
            Func<GameFlowDomain> getFlow,
            Func<bool> hasLogicSession,
            Func<float> getFixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Action<IEntity> destroyEntityTree)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _getPlan = getPlan ?? throw new ArgumentNullException(nameof(getPlan));
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _getFlow = getFlow ?? throw new ArgumentNullException(nameof(getFlow));
            _hasLogicSession = hasLogicSession ?? throw new ArgumentNullException(nameof(hasLogicSession));
            _getFixedDeltaSeconds = getFixedDeltaSeconds ??
                throw new ArgumentNullException(nameof(getFixedDeltaSeconds));
            _resolveIdealFrameLimit = resolveIdealFrameLimit ??
                throw new ArgumentNullException(nameof(resolveIdealFrameLimit));
            _destroyEntityTree = destroyEntityTree ??
                throw new ArgumentNullException(nameof(destroyEntityTree));
        }

        public void StartRemoteDrivenLocalWorld()
        {
            _runtime.Simulation.StartRemoteDriven(
                _getPlan(),
                _getContext(),
                _getFixedDeltaSeconds(),
                _resolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => _runtime.Diagnostics.ShouldForceClientHashMismatch);
#else
                () => false);
#endif
        }

        public void StartConfirmedAuthorityWorld()
        {
            _runtime.Simulation.StartConfirmedAuthority(
                _getPlan(),
                _getContext(),
                _getFlow(),
                _hasLogicSession(),
                _getFixedDeltaSeconds(),
                _resolveIdealFrameLimit,
                _destroyEntityTree);
        }

        public void DisposeReplayRecordWriter() => _runtime.Replay.DisposeRecordWriter();

        public Task StopRecoveryAsync() =>
            _runtime.Recovery?.StopAsync() ?? Task.CompletedTask;

        public void TryDestroyBattleWorlds() =>
            _runtime.Simulation.DestroyBattleWorlds(_getPlan());

        public void DisposeSnapshotRouting() => _runtime.SnapshotRouting.Dispose();

        public void DisposeConfirmedView() =>
            _runtime.Simulation.DisposeConfirmedView(_getFlow(), _destroyEntityTree);

        public void DisposeRemoteDrivenWorld() =>
            _runtime.Simulation.DisposeRemoteDrivenWorld();

        public void DisposeConfirmedWorld() =>
            _runtime.Simulation.DisposeConfirmedWorld(_getContext());

        public void DisposeRemoteInterpolation()
        {
            _runtime.DisposeReplication();
            var context = _getContext();
            if (context != null) context.CanSubmitGameplayInput = true;
        }

        public void ResetSessionHandles() => _runtime.Handles.ResetSessionResources();
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
    }
}
