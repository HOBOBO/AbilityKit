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

        private readonly BattleSessionState _state = new BattleSessionState();
        private readonly BattleSessionHandles _handles = new BattleSessionHandles();

        private BattleViewFeature _replayViewFeature;
        private BattleHudFeature _replayHudFeature;
        private bool _renderReplayPresentation = true;
        private bool _replayPresentationDetached;

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
            IBattleSessionGatewayRoomClientFactory gatewayRoomClientFactory = null)
        {
            _bootstrapper = bootstrapper;
            _connectionRegistry = connectionRegistry ?? new AbilityKitConnectionRegistry();
            _worldInstaller = worldInstaller ?? new DefaultBattleSessionWorldInstaller();
            _transportFactory = transportFactory ?? new DefaultBattleSessionTransportFactory();
            _gatewayConnectionFactory = gatewayRoomConnectionFactory ?? new DefaultBattleSessionGatewayConnectionFactory(gatewayConnectionFactory);
            _gatewayRoomClientFactory = gatewayRoomClientFactory ?? new DefaultBattleSessionGatewayRoomClientFactory();
            _orchestrator = new SessionOrchestrator(_state, _handles, this);
            _dispatchers = new SessionDispatchersController();
            _net = new SessionNetAdapterController();
            _replayCtrl = new SessionReplayController();
            _planCtrl = new SessionPlanController();
            _eventsCtrl = new SessionEventsController();
            _tickLoop = new TickLoopController(_state, _handles, this);
            _snapshotRouting = new SessionSnapshotRoutingController();
            _worldCatchUp = new SessionWorldCatchUpController();
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        public bool IsReplaySession => _handles.Replay.Driver != null;
        public bool IsPlaying => _handles.Replay.Driver?.IsPlaying ?? false;
        public bool RenderPresentation => !IsReplaySession || _renderReplayPresentation;
        public int CurrentFrame => _lastFrame;
        int Battle.Replay.IBattleReplayControl.LastFrame => _handles.Replay.Driver?.LastFrame ?? 0;
        public string ReplayPath => IsReplaySession ? _plan.RunModeOptions.InputReplayPath : string.Empty;

        public float PlaybackSpeed
        {
            get => _handles.Replay.Driver?.PlaybackSpeed ?? 1f;
            set
            {
                if (_handles.Replay.Driver != null) _handles.Replay.Driver.PlaybackSpeed = value;
            }
        }

        public bool TryLoad(string path, bool renderPresentation, out string error)
        {
            error = null;
            if (_session == null)
            {
                error = "当前没有活动中的 Battle Session，无法复用战斗启动配置。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "录像文件不存在。";
                return false;
            }

            try
            {
                var file = FrameRecordCodecs.Current.Load(path);
                if (file == null)
                {
                    error = "录像文件为空或格式不受支持。";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"录像解码失败：{ex.Message}";
                return false;
            }

            var previousPlan = _plan;
            var previousRenderPresentation = RenderPresentation;
            try
            {
                SuspendReplayPresentation();
                _renderReplayPresentation = renderPresentation;
                _plan = previousPlan.WithInputReplay(path);
                StopSession();
                StartSession();
                ApplyAutoPlanActions();

                var replay = _handles.Replay.Driver;
                if (replay == null)
                {
                    throw new InvalidOperationException("Replay Driver 创建失败。");
                }

                replay.Pause();
                RestoreReplayPresentationIfEnabled();
                return true;
            }
            catch (Exception ex)
            {
                error = $"启动 Replay Session 失败：{ex.Message}";
                _renderReplayPresentation = previousRenderPresentation;
                TryRestoreSession(previousPlan);
                RestoreReplayPresentationIfEnabled();
                return false;
            }
        }

        public void Play()
        {
            _handles.Replay.Driver?.Play();
        }

        public void Pause()
        {
            _handles.Replay.Driver?.Pause();
            _tickAcc = 0f;
        }

        public bool StepForward()
        {
            Pause();
            return SeekToFrame(_lastFrame + 1);
        }

        public bool StepBackward()
        {
            Pause();
            return SeekToFrame(_lastFrame - 1);
        }

        public bool SeekToFrame(int frame)
        {
            return _replayCtrl.SeekToFrame(_plan, _state, _handles, _ctx, this, frame);
        }

        private void SuspendReplayPresentation()
        {
            if (_replayPresentationDetached || _flow == null) return;

            if (_replayViewFeature == null) _phaseCtx.Features.TryGet(out _replayViewFeature);
            if (_replayHudFeature == null) _phaseCtx.Features.TryGet(out _replayHudFeature);

            if (_replayHudFeature != null) _flow.Detach(_replayHudFeature);
            if (_replayViewFeature != null) _flow.Detach(_replayViewFeature);
            _replayPresentationDetached = true;
        }

        private void RestoreReplayPresentationIfEnabled()
        {
            if (!_renderReplayPresentation || !_replayPresentationDetached || _flow == null) return;

            if (_replayViewFeature != null) _flow.Attach(_replayViewFeature);
            if (_replayHudFeature != null) _flow.Attach(_replayHudFeature);
            _replayPresentationDetached = false;
        }

        private void ResetReplayPresentationState()
        {
            _replayViewFeature = null;
            _replayHudFeature = null;
            _renderReplayPresentation = true;
            _replayPresentationDetached = false;
        }

        private void TryRestoreSession(BattleStartPlan plan)
        {
            try
            {
                _plan = plan;
                StopSession();
                StartSession();
                ApplyAutoPlanActions();
            }
            catch
            {
                StopSession();
            }
        }

        private float GetFixedDeltaSeconds() => _orchestrator.GetFixedDeltaSeconds();

        private void StartSession() => _orchestrator.StartSession();

        private void StopSession() => _orchestrator.StopSession();
    }
}
