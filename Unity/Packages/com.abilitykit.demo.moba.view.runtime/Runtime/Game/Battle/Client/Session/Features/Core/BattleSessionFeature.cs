using System;
using System.IO;
using System.Threading.Tasks;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature :
        IBattleSessionFeature,
        Battle.Replay.IBattleReplayControl,
        IBattleAssetLoadSessionPort,
        ISessionLogicPort,
        ISessionPipelinePort,
        ISessionSimulationHost
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DebugForceClientHashMismatch
        {
            get => BattleSessionDiagnostics.DebugForceClientHashMismatch;
            set => BattleSessionDiagnostics.DebugForceClientHashMismatch = value;
        }

        public static bool TryGetDebugForceClientHashMismatch(BattleContext context, out bool enabled) =>
            BattleSessionDiagnostics.TryGetDebugForceClientHashMismatch(context, out enabled);

        public static bool TrySetDebugForceClientHashMismatch(BattleContext context, bool enabled) =>
            BattleSessionDiagnostics.TrySetDebugForceClientHashMismatch(context, enabled);
#endif

        private readonly IBattleBootstrapper _bootstrapper;
        private readonly IAbilityKitConnectionRegistry _connectionRegistry;
        private readonly IBattleSessionGatewayConnectionFactory _gatewayConnectionFactory;
        private readonly IBattleSessionGatewayRoomClientFactory _gatewayRoomClientFactory;
        private readonly IBattleLogicSessionRegistry _sessionRegistry;
        private readonly BattleSessionConnectionRuntime _connectionRuntime;
        private readonly BattleClientSessionDriver _clientDriver;

        // Compatibility facade during staged migration. Mutable session state and resources
        // are owned by BattleSessionRuntime rather than by this feature facade.
        private readonly BattleSessionRuntime _runtime = new BattleSessionRuntime();
        private readonly SessionModuleRuntime _moduleRuntime;
        private BattleSessionState _state => _runtime.State;
        private BattleSessionHandles _handles => _runtime.Handles;
        private SessionOrchestrator _orchestrator => _runtime.Orchestrator;

        private readonly SessionRuntimeResourcesPort _runtimeResourcesPort;
        private readonly SessionLifecycleHost _lifecycleHost;
        private readonly SessionNetAdapterContextHost _netAdapterContextHost;
        private SessionDispatchersController _dispatchers => _runtime.Dispatchers;
        private SessionEventsController _eventsCtrl => _runtime.EventsController;
        private SessionTickLoopController _tickLoop => _runtime.TickLoop;
        private SessionWorldCatchUpController _worldCatchUp => _runtime.WorldCatchUp;
        private readonly BattleSessionLifecycleController<GamePhaseContext> _featureLifecycle;
        private SessionStartupTaskOwner _startupTasks => _runtime.StartupTasks;

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
            IBattleLogicSessionRegistry sessionRegistry = null,
            Func<string, ISessionSubFeature<ISessionRuntimeFeature>> sessionModuleFactory = null)
        {
            _bootstrapper = bootstrapper;
            _moduleRuntime = new SessionModuleRuntime(this);
            _connectionRegistry = connectionRegistry ?? new AbilityKitConnectionRegistry();
            var effectiveTransportFactory = transportFactory ?? new DefaultBattleSessionTransportFactory();
            _gatewayConnectionFactory = gatewayRoomConnectionFactory ?? new DefaultBattleSessionGatewayConnectionFactory(gatewayConnectionFactory);
            _gatewayRoomClientFactory = gatewayRoomClientFactory ?? new DefaultBattleSessionGatewayRoomClientFactory();
            _sessionRegistry = sessionRegistry ?? new DefaultBattleLogicSessionRegistry();
            _connectionRuntime = new BattleSessionConnectionRuntime(
                effectiveTransportFactory, _sessionRegistry, NetworkCondition,
                _runtime.InputSubmissionDiagnostics);
            _sessionModuleFactory = sessionModuleFactory;
            _runtime.ConfigureSimulation(worldInstaller ?? new DefaultBattleSessionWorldInstaller());
            _runtime.ConfigureSimulationController(this);
            _runtime.ConfigureGatewayRoom(
                _connectionRegistry,
                _gatewayConnectionFactory,
                _gatewayRoomClientFactory,
                NetworkCondition);
            _runtimeResourcesPort = new SessionRuntimeResourcesPort(_runtime);
            _lifecycleHost = new SessionLifecycleHost(
                _handles,
                this,
                this,
                _runtimeResourcesPort);
            _runtime.ConfigureOrchestrator(_lifecycleHost);
            _featureLifecycle = new BattleSessionLifecycleController<GamePhaseContext>(
                CreateTeardownSteps);
            _runtime.ConfigureTickLoop(this);
            _clientDriver = new BattleClientSessionDriver(new SessionDriverHost(this));
            _clientDriver.AttachProjection("unity-primary",
                new UnityBattleClientProjection(
                    () => _ctx,
                    () => _plan.Authority.EnableClientPrediction));
            _netAdapterContextHost = new SessionNetAdapterContextHost(
                () => _plan,
                _handles,
                () => _snapshots);
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        BattleStartPlan ISessionSimulationHost.Plan => _plan;
        BattleContext ISessionSimulationHost.Context => _ctx;
        IBattleViewFeatureHost ISessionSimulationHost.Flow => _flow;
        bool ISessionSimulationHost.HasLogicSession => _session != null;
        float ISessionSimulationHost.FixedDeltaSeconds => GetFixedDeltaSeconds();
        int ISessionSimulationHost.ResolveIdealFrameLimit(AbilityKit.Ability.World.Abstractions.WorldId worldId) =>
            ResolveIdealFrameLimit(worldId);
        void ISessionSimulationHost.DestroyEntityTree(AbilityKit.World.ECS.IEntity root) =>
            DestroyEntityTree(root);

        public bool IsReplaySession => _runtime.Replay.IsActive;
        public bool IsPlaying => _runtime.Replay.IsPlaying;
        public bool RenderPresentation => IsReplaySession
            ? _replayPresentationInstanceId != null
            : HasActiveBattleView();
        public int CurrentFrame => IsReplaySession ? _runtime.Replay.CurrentFrame : _lastFrame;
        int Battle.Replay.IBattleReplayControl.LastFrame => _runtime.Replay.LastFrame;
        public string ReplayPath => _runtime.Replay.ReplayPath;

        public float PlaybackSpeed
        {
            get => _runtime.Replay.PlaybackSpeed;
            set => _runtime.Replay.PlaybackSpeed = value;
        }

        public bool TryLoad(string path, bool renderPresentation, out string error)
            => TryLoad(_plan, path, renderPresentation, out error);

        public bool TryLoad(BattleStartPlan plan, string path,
            bool renderPresentation, out string error)
        {
            if (string.IsNullOrWhiteSpace(plan.World.WorldId))
            {
                error = "Replay requires a battle start plan with a world ID.";
                return false;
            }

            if (renderPresentation && (_flow == null || _ctx == null))
            {
                error = "Replay presentation requires an attached battle view host.";
                return false;
            }

            var otherProjectionCount = _runtime.Presentation.ProjectionViewCount -
                (_replayPresentationInstanceId != null ? 1 : 0);
            if (renderPresentation && (HasActiveBattleView() || otherProjectionCount > 0))
            {
                error = "An active battle view or projection requires explicit replay placement. " +
                    "Load without presentation, then call ShowReplayView with a nonzero offset.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "录像文件不存在。";
                return false;
            }

            if (!_runtime.Replay.TryStart(plan, path, out error)) return false;
            HideReplayView();
            if (!renderPresentation) return true;
            try
            {
                if (ShowReplayView("Replay", UnityEngine.Vector3.zero)) return true;
                error = "Replay world cannot be projected by the current battle view host.";
            }
            catch (Exception exception)
            {
                error = "Replay presentation failed: " + exception.Message;
            }
            HideReplayView();
            _runtime.Replay.Stop();
            return false;
        }

        public void Play()
        {
            _runtime.Replay.Play();
        }

        public void Pause()
        {
            _runtime.Replay.Pause();
        }

        public bool StepForward()
        {
            Pause();
            return SeekToFrame(_runtime.Replay.CurrentFrame + 1);
        }

        public bool StepBackward()
        {
            Pause();
            return SeekToFrame(_runtime.Replay.CurrentFrame - 1);
        }

        public bool SeekToFrame(int frame)
        {
            var result = _runtime.Replay.SeekToFrame(frame);
            if (result) _replayProjectionSource?.Invalidate();
            return result;
        }

        private float GetFixedDeltaSeconds() => _orchestrator.GetFixedDeltaSeconds();

        private bool HasActiveBattleView() =>
            (_phaseCtx.Features != null &&
             _phaseCtx.Features.TryGet(out BattleViewFeature primary) && primary != null) ||
            _runtime.Presentation.ConfirmedFeature != null;

        private Task StartSessionAsync() => _clientDriver.StartAsync();

        private Task StopSessionAsync() => _clientDriver.StopAsync();
    }
}
