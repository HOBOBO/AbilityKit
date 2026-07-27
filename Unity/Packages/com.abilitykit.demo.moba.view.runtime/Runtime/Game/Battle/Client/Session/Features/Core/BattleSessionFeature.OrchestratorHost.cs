using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    // Isolates SessionOrchestrator from the feature's broader runtime and presentation interfaces.
    internal sealed class SessionLifecycleHost : ISessionOrchestratorHost
    {
        private readonly Func<BattleStartPlan> _getPlan;
        private readonly Func<BattleContext> _getContext;
        private readonly BattleSessionHandles _handles;
        private readonly Func<Action<FramePacket>> _getFrameReceivedHandler;
        private readonly Func<BattleLogicSessionOptions, BattleLogicSession> _startBattleLogicSession;
        private readonly Action _stopBattleLogicSession;
        private readonly Action _invokeSessionStartingPipeline;
        private readonly Action _invokeSessionStoppingPipeline;
        private readonly Action _invokeReplaySetupPipeline;
        private readonly Action _startRemoteDrivenLocalWorld;
        private readonly Action _startConfirmedAuthorityWorld;
        private readonly Action _tryDestroyBattleWorlds;
        private readonly Action _disposeSnapshotRouting;
        private readonly Action _disposeConfirmedView;
        private readonly Action _disposeRemoteDrivenWorld;
        private readonly Action _disposeConfirmedWorld;
        private readonly Action _disposeRemoteInterpolation;
        private readonly Action _resetSessionHandles;

        public SessionLifecycleHost(
            Func<BattleStartPlan> getPlan,
            Func<BattleContext> getContext,
            BattleSessionHandles handles,
            Func<Action<FramePacket>> getFrameReceivedHandler,
            Func<BattleLogicSessionOptions, BattleLogicSession> startBattleLogicSession,
            Action stopBattleLogicSession,
            Action invokeSessionStartingPipeline,
            Action invokeSessionStoppingPipeline,
            Action invokeReplaySetupPipeline,
            Action startRemoteDrivenLocalWorld,
            Action startConfirmedAuthorityWorld,
            Action tryDestroyBattleWorlds,
            Action disposeSnapshotRouting,
            Action disposeConfirmedView,
            Action disposeRemoteDrivenWorld,
            Action disposeConfirmedWorld,
            Action disposeRemoteInterpolation,
            Action resetSessionHandles)
        {
            _getPlan = getPlan;
            _getContext = getContext;
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _getFrameReceivedHandler = getFrameReceivedHandler;
            _startBattleLogicSession = startBattleLogicSession;
            _stopBattleLogicSession = stopBattleLogicSession;
            _invokeSessionStartingPipeline = invokeSessionStartingPipeline;
            _invokeSessionStoppingPipeline = invokeSessionStoppingPipeline;
            _invokeReplaySetupPipeline = invokeReplaySetupPipeline;
            _startRemoteDrivenLocalWorld = startRemoteDrivenLocalWorld;
            _startConfirmedAuthorityWorld = startConfirmedAuthorityWorld;
            _tryDestroyBattleWorlds = tryDestroyBattleWorlds;
            _disposeSnapshotRouting = disposeSnapshotRouting;
            _disposeConfirmedView = disposeConfirmedView;
            _disposeRemoteDrivenWorld = disposeRemoteDrivenWorld;
            _disposeConfirmedWorld = disposeConfirmedWorld;
            _disposeRemoteInterpolation = disposeRemoteInterpolation;
            _resetSessionHandles = resetSessionHandles;
        }

        public BattleStartPlan Plan => _getPlan();
        public BattleContext Context => _getContext();

        public void StartBattleLogicSession(BattleLogicSessionOptions options)
        {
            _handles.Session = _startBattleLogicSession(options)
                ?? throw new InvalidOperationException("Battle logic session host returned null.");
        }

        public void SubscribeFrameReceived()
        {
            var session = _handles.Session
                ?? throw new InvalidOperationException("Cannot subscribe before the battle logic session is available.");
            session.FrameReceived += _getFrameReceivedHandler();
        }

        public void UnsubscribeFrameReceived()
        {
            var session = _handles.Session;
            if (session != null) session.FrameReceived -= _getFrameReceivedHandler();
        }

        public void StopBattleLogicSession() => _stopBattleLogicSession();
        public void InvokeSessionStartingPipeline() => _invokeSessionStartingPipeline();
        public void InvokeSessionStoppingPipeline() => _invokeSessionStoppingPipeline();
        public void InvokeReplaySetupPipeline() => _invokeReplaySetupPipeline();
        public void StartRemoteDrivenLocalWorld() => _startRemoteDrivenLocalWorld();
        public void StartConfirmedAuthorityWorld() => _startConfirmedAuthorityWorld();
        public void TryDestroyBattleWorlds() => _tryDestroyBattleWorlds();
        public void DisposeSnapshotRouting() => _disposeSnapshotRouting();
        public void DisposeConfirmedView() => _disposeConfirmedView();
        public void DisposeRemoteDrivenWorld() => _disposeRemoteDrivenWorld();
        public void DisposeConfirmedWorld() => _disposeConfirmedWorld();
        public void DisposeRemoteInterpolation() => _disposeRemoteInterpolation();
        public void ResetSessionHandles() => _resetSessionHandles();
    }
}
