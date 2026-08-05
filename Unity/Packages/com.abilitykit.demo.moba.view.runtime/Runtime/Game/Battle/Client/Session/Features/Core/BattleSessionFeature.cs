using System;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature : IBattleSessionFeature, Battle.Replay.IBattleReplayControl
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DebugForceClientHashMismatch { get; set; }
#endif

        private readonly IBattleBootstrapper _bootstrapper;
        private readonly IAbilityKitConnectionRegistry _connectionRegistry;
        private readonly IBattleSessionWorldInstaller _worldInstaller;
        private readonly IBattleSessionTransportFactory _transportFactory;
        private readonly IBattleSessionGatewayConnectionFactory _gatewayConnectionFactory;
        private readonly IBattleSessionGatewayRoomClientFactory _gatewayRoomClientFactory;
        private readonly IBattleLogicSessionRegistry _sessionRegistry;
        private readonly BattleReplaySessionOwner _replayOwner = new BattleReplaySessionOwner();

        private readonly BattleSessionState _state = new BattleSessionState();
        private readonly BattleSessionHandles _handles = new BattleSessionHandles();

        private readonly SessionLifecycleHost _lifecycleHost;
        private readonly TickLoopHost _tickLoopHost;
        private readonly SessionNetAdapterContextHost _netAdapterContextHost;
        private readonly SessionOrchestrator _orchestrator;
        private readonly SessionDispatchersController _dispatchers;
        private readonly SessionNetAdapterController _net;
        private readonly SessionReplayController _replayCtrl;
        private readonly SessionPlanController _planCtrl;
        private readonly SessionEventsController _eventsCtrl;
        private readonly TickLoopController _tickLoop;
        private readonly SessionSnapshotRoutingController _snapshotRouting;
        private readonly SessionWorldCatchUpController _worldCatchUp;

#if UNITY_EDITOR
        private static bool _editorPlayModeHookInstalled;
#endif

        public BattleSessionFeature(
            IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, IConnection> gatewayConnectionFactory = null,
            IAbilityKitConnectionRegistry connectionRegistry = null)
            : this(
                bootstrapper,
                gatewayConnectionFactory,
                connectionRegistry,
                new DefaultBattleSessionWorldInstaller(),
                new DefaultBattleSessionTransportFactory(),
                new DefaultBattleSessionGatewayConnectionFactory(gatewayConnectionFactory),
                new DefaultBattleSessionGatewayRoomClientFactory())
        {
        }

        internal BattleSessionFeature(
            IBattleBootstrapper bootstrapper,
            Func<BattleStartPlan, IConnection> gatewayConnectionFactory,
            IAbilityKitConnectionRegistry connectionRegistry,
            IBattleSessionWorldInstaller worldInstaller,
            IBattleSessionTransportFactory transportFactory = null,
            IBattleSessionGatewayConnectionFactory gatewayRoomConnectionFactory = null,
            IBattleSessionGatewayRoomClientFactory gatewayRoomClientFactory = null,
            IBattleLogicSessionRegistry sessionRegistry = null)
        {
            _bootstrapper = bootstrapper;
            _connectionRegistry = connectionRegistry ?? new AbilityKitConnectionRegistry();
            _worldInstaller = worldInstaller ?? new DefaultBattleSessionWorldInstaller();
            _transportFactory = transportFactory ?? new DefaultBattleSessionTransportFactory();
            _gatewayConnectionFactory = gatewayRoomConnectionFactory ?? new DefaultBattleSessionGatewayConnectionFactory(gatewayConnectionFactory);
            _gatewayRoomClientFactory = gatewayRoomClientFactory ?? new DefaultBattleSessionGatewayRoomClientFactory();
            _sessionRegistry = sessionRegistry ?? new DefaultBattleLogicSessionRegistry();
            _lifecycleHost = new SessionLifecycleHost(new SessionLifecycleHostOptions
            {
                GetPlan = () => _plan,
                GetContext = () => _ctx,
                Handles = _handles,
                GetFrameReceivedHandler = () => OnFrame,
                StartBattleLogicSession = StartBattleLogicSession,
                StopBattleLogicSession = _sessionRegistry.Stop,
                InvokeSessionStartingPipeline = InvokeSessionStartingPipeline,
                InvokeSessionStoppingPipeline = InvokeSessionStoppingPipeline,
                InvokeReplaySetupPipeline = InvokeReplaySetupPipeline,
                StartRemoteDrivenLocalWorld = StartRemoteDrivenLocalWorld,
                StartConfirmedAuthorityWorld = StartConfirmedAuthorityWorld,
                TryDestroyBattleWorlds = TryDestroyBattleWorlds,
                DisposeSnapshotRouting = DisposeSnapshotRouting,
                DisposeConfirmedView = DisposeConfirmedView,
                DisposeRemoteDrivenWorld = DisposeRemoteDrivenWorld,
                DisposeConfirmedWorld = DisposeConfirmedWorld,
                DisposeRemoteInterpolation = DisposeRemoteInterpolation,
                ResetSessionHandles = ResetSessionHandles,
            });
            _orchestrator = new SessionOrchestrator(_state, _handles, _lifecycleHost);
            _dispatchers = new SessionDispatchersController();
            _net = new SessionNetAdapterController();
            _replayCtrl = new SessionReplayController();
            _planCtrl = new SessionPlanController();
            _eventsCtrl = new SessionEventsController();
            _tickLoopHost = new TickLoopHost(
                GetFixedDeltaSeconds,
                TickRemoteDrivenLocalSim,
                TickConfirmedAuthorityWorldSim,
                TickRemoteInterpolation);
            _tickLoop = new TickLoopController(_state, _handles, _tickLoopHost);
            _netAdapterContextHost = new SessionNetAdapterContextHost(
                () => _plan,
                _handles,
                () => _snapshots);
            _snapshotRouting = new SessionSnapshotRoutingController();
            _worldCatchUp = new SessionWorldCatchUpController();
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        public bool IsReplaySession => _replayOwner.IsActive;
        public bool IsPlaying => _replayOwner.IsPlaying;
        public bool RenderPresentation => true;
        public int CurrentFrame => IsReplaySession ? _replayOwner.CurrentFrame : _lastFrame;
        int Battle.Replay.IBattleReplayControl.LastFrame => _replayOwner.LastFrame;
        public string ReplayPath => _replayOwner.ReplayPath;

        public float PlaybackSpeed
        {
            get => _replayOwner.PlaybackSpeed;
            set => _replayOwner.PlaybackSpeed = value;
        }

        public bool TryLoad(string path, bool renderPresentation, out string error)
        {
            if (_session == null)
            {
                error = "当前没有活动中的 Battle Session，无法复用战斗启动配置。";
                return false;
            }

            if (renderPresentation)
            {
                error = "当前回放仅支持独立逻辑会话；呈现回放需要独立 BattleContext 资源池。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "录像文件不存在。";
                return false;
            }

            return _replayOwner.TryStart(_plan, path, out error);
        }

        public void Play()
        {
            _replayOwner.Play();
        }

        public void Pause()
        {
            _replayOwner.Pause();
        }

        public bool StepForward()
        {
            Pause();
            return SeekToFrame(_replayOwner.CurrentFrame + 1);
        }

        public bool StepBackward()
        {
            Pause();
            return SeekToFrame(_replayOwner.CurrentFrame - 1);
        }

        public bool SeekToFrame(int frame)
        {
            return _replayOwner.SeekToFrame(frame);
        }

        private float GetFixedDeltaSeconds() => _orchestrator.GetFixedDeltaSeconds();

        private void StartSession() => _orchestrator.StartSession();

        private void StopSession() => _orchestrator.StopSession();
    }
}
