using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    // Isolates SessionOrchestrator from the feature's broader runtime and presentation interfaces.

    /// <summary>
    /// Options object consolidating the 18 delegate/property dependencies of
    /// <see cref="SessionLifecycleHost"/>. Extracted from the constructor to keep
    /// the signature readable and allow future additions without cascading changes.
    /// </summary>
    internal sealed class SessionLifecycleHostOptions
    {
        public Func<BattleStartPlan> GetPlan { get; set; }
        public Func<BattleContext> GetContext { get; set; }
        public BattleSessionHandles Handles { get; set; }
        public Func<Action<FramePacket>> GetFrameReceivedHandler { get; set; }
        public Func<BattleLogicSessionOptions, BattleLogicSession> StartBattleLogicSession { get; set; }
        public Action StopBattleLogicSession { get; set; }
        public Action InvokeSessionStartingPipeline { get; set; }
        public Action InvokeSessionStoppingPipeline { get; set; }
        public Action InvokeReplaySetupPipeline { get; set; }
        public Action StartRemoteDrivenLocalWorld { get; set; }
        public Action StartConfirmedAuthorityWorld { get; set; }
        public Action TryDestroyBattleWorlds { get; set; }
        public Action DisposeSnapshotRouting { get; set; }
        public Action DisposeConfirmedView { get; set; }
        public Action DisposeRemoteDrivenWorld { get; set; }
        public Action DisposeConfirmedWorld { get; set; }
        public Action DisposeRemoteInterpolation { get; set; }
        public Action ResetSessionHandles { get; set; }
    }

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

        public SessionLifecycleHost(SessionLifecycleHostOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _getPlan = options.GetPlan;
            _getContext = options.GetContext;
            _handles = options.Handles ?? throw new ArgumentNullException(nameof(options.Handles));
            _getFrameReceivedHandler = options.GetFrameReceivedHandler;
            _startBattleLogicSession = options.StartBattleLogicSession;
            _stopBattleLogicSession = options.StopBattleLogicSession;
            _invokeSessionStartingPipeline = options.InvokeSessionStartingPipeline;
            _invokeSessionStoppingPipeline = options.InvokeSessionStoppingPipeline;
            _invokeReplaySetupPipeline = options.InvokeReplaySetupPipeline;
            _startRemoteDrivenLocalWorld = options.StartRemoteDrivenLocalWorld;
            _startConfirmedAuthorityWorld = options.StartConfirmedAuthorityWorld;
            _tryDestroyBattleWorlds = options.TryDestroyBattleWorlds;
            _disposeSnapshotRouting = options.DisposeSnapshotRouting;
            _disposeConfirmedView = options.DisposeConfirmedView;
            _disposeRemoteDrivenWorld = options.DisposeRemoteDrivenWorld;
            _disposeConfirmedWorld = options.DisposeConfirmedWorld;
            _disposeRemoteInterpolation = options.DisposeRemoteInterpolation;
            _resetSessionHandles = options.ResetSessionHandles;
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
