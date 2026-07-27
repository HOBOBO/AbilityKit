#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Demo.Shooter.View.Network;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public sealed class ShooterPlaySessionRunner : IDisposable
    {
        private const int MaxCatchUpTicksPerRender = 2;
        private const int HighDensityPresentationBaselineIntervalSeconds = 5;

        private readonly IShooterHostInputSource _inputSource;
        private readonly IShooterHostViewSink _viewSink;
        private ShooterAcceptanceSession? _session;
        private ShooterPlayModeSessionOptions _options;
        private ShooterHostFrameInput _lastInput;
        private ShooterClientInputSubmitResult _lastSubmitResult;
        private ShooterClientFrameTickResult _lastTickResult;
        private int _lastAuthorityAcceptedInputs;
        private float _accumulator;
        private long _stepCount;
        private long _renderCount;
        private ShooterTimeAnchorCoordinator? _timeAnchors;
        private long _droppedCatchUpTicks;
        private int _presentationSnapshotIntervalTicks = 1;
        private bool _usePureStatePresentationSnapshots;
        private ShooterPureStateSyncSettings _presentationPureStateSettings = ShooterPureStateSyncSettings.Default;
        private bool _hasPresentationPureStateBaseline;
        private int _presentationPureStateBaselineFrame;
        private uint _presentationPureStateBaselineHash;

        public ShooterPlaySessionRunner(IShooterHostInputSource inputSource, IShooterHostViewSink viewSink)
        {
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            _viewSink = viewSink ?? throw new ArgumentNullException(nameof(viewSink));
            _options = ShooterPlayModeSessionOptions.Default;
        }

        public event Action<ShooterAcceptanceSession?>? SessionChanged;

        public bool IsRunning => _session != null;
        public ShooterAcceptanceSession? Session => _session;
        public ShooterPlayModeSessionOptions Options => _options;
        public ShooterHostFrameInput LastInput => _lastInput;
        public ShooterClientInputSubmitResult LastSubmitResult => _lastSubmitResult;
        public ShooterClientFrameTickResult LastTickResult => _lastTickResult;
        public int LastAuthorityAcceptedInputs => _lastAuthorityAcceptedInputs;
        public long StepCount => _stepCount;
        public long RenderCount => _renderCount;
        public SyncTimeAnchor LastLocalTimeAnchor => _timeAnchors?.LastLocalAnchor ?? default;
        public long DroppedCatchUpTicks => _droppedCatchUpTicks;
        public int PresentationSnapshotIntervalTicks => _presentationSnapshotIntervalTicks;

        public ShooterAcceptanceSession Start(ShooterPlayModeSessionOptions options)
        {
            Stop();

            _options = AlignWithGameplayScenario(options.Normalized());

            var profile = CreateProfile(_options);
            var players = new List<ShooterStartPlayer>(_options.PlayerCount);
            for (var i = 0; i < _options.PlayerCount; i++)
            {
                players.Add(new ShooterStartPlayer(i + 1, $"P{i + 1}", i * 4f, 0f));
            }

            _session = ShooterAcceptanceLab.Create(
                _options.SyncModel,
                profile,
                networkName: _options.NetworkName,
                tickRate: _options.TickRate,
                players: players,
                randomSeed: _options.RandomSeed,
                enableAuthoritativeWorld: _options.EnableAuthoritativeWorld,
                gameplayScenario: _options.GameplayScenario);

            _session.Presentation.ControlledPlayerId = _options.ControlledPlayerId;
            if (_session.AuthoritativePresentation != null)
            {
                _session.AuthoritativePresentation.ControlledPlayerId = _options.ControlledPlayerId;
            }

            ShooterNetworkConditionRegistry.Builtin.ApplyProfile(profile);
            _lastInput = default;
            _lastSubmitResult = default;
            _lastTickResult = default;
            _lastAuthorityAcceptedInputs = 0;
            _accumulator = 0f;
            _stepCount = 0;
            _renderCount = 0;
            _timeAnchors = ShooterTimeAnchorCoordinator.CreateLocal(_options.TickRate);
            _droppedCatchUpTicks = 0;
            _presentationSnapshotIntervalTicks = DeterminePresentationSnapshotIntervalTicks(_options);
            _usePureStatePresentationSnapshots = ShouldUsePureStatePresentationSnapshots(_options);
            _presentationPureStateSettings = CreatePresentationPureStateSettings(_options);
            _hasPresentationPureStateBaseline = false;
            _presentationPureStateBaselineFrame = 0;
            _presentationPureStateBaselineHash = 0u;
            SessionChanged?.Invoke(_session);
            return _session;
        }

        public void Stop()
        {
            if (_session == null)
            {
                _viewSink.Clear();
                return;
            }

            var session = _session;
            _session = null;
            _lastInput = default;
            _lastSubmitResult = default;
            _lastTickResult = default;
            _lastAuthorityAcceptedInputs = 0;
            _accumulator = 0f;
            _stepCount = 0;
            _renderCount = 0;
            _timeAnchors = null;
            _droppedCatchUpTicks = 0;
            _presentationSnapshotIntervalTicks = 1;
            _usePureStatePresentationSnapshots = false;
            _presentationPureStateSettings = ShooterPureStateSyncSettings.Default;
            _hasPresentationPureStateBaseline = false;
            _presentationPureStateBaselineFrame = 0;
            _presentationPureStateBaselineHash = 0u;
            session.Dispose();
            _viewSink.Clear();
            SessionChanged?.Invoke(null);
        }

        public void Tick(float deltaSeconds)
        {
            if (_session == null)
            {
                return;
            }

            var tickInterval = 1f / _options.TickRate;
            _accumulator += Math.Max(0f, deltaSeconds);

            var ticksThisRender = 0;
            while (_accumulator >= tickInterval && ticksThisRender++ < MaxCatchUpTicksPerRender)
            {
                _accumulator -= tickInterval;
                StepOnce(tickInterval);
            }

            if (_accumulator >= tickInterval)
            {
                var skippedTicks = (long)(_accumulator / tickInterval);
                _droppedCatchUpTicks += skippedTicks;
                _accumulator = 0f;
            }

            RenderLatest();
        }

        public void ApplyNetwork(NetworkConditionProfile profile)
        {
            _session?.ApplyNetwork(profile);
        }

        public void Dispose()
        {
            Stop();
            SessionChanged = null;
        }

        private void StepOnce(float deltaSeconds)
        {
            if (_session == null)
            {
                return;
            }

            (_timeAnchors ??= ShooterTimeAnchorCoordinator.CreateLocal(_options.TickRate)).AdvanceLocal();
            var input = _inputSource.ReadInput(_options.ControlledPlayerId);
            _lastInput = input;
            _stepCount++;
            var command = ShooterClientInputBuilder.CreateCommand(
                _options.ControlledPlayerId,
                input.MoveX,
                input.MoveY,
                input.AimX,
                input.AimY,
                input.Fire,
                input.AttackSlot);

            _lastSubmitResult = _session.Controller.SubmitLocalInput(in command);
            _lastTickResult = _session.Controller.Tick(deltaSeconds);

            _lastAuthorityAcceptedInputs = 0;
            if (_session.AuthoritativeWorld != null)
            {
                _session.EnqueueAuthoritativeInput(_lastSubmitResult.RequestedFrame, in command);
                _session.TickAuthoritativeWorld(deltaSeconds);
                _lastAuthorityAcceptedInputs = _session.LastAuthorityDeliveredInputCount;
            }

            if (ShouldPublishPresentationSnapshot())
            {
                PublishPresentationSnapshot();
            }
        }

        private void PublishPresentationSnapshot()
        {
            if (_session == null)
            {
                return;
            }

            if (_usePureStatePresentationSnapshots)
            {
                var isFullBaseline = ShouldPublishPresentationPureStateBaseline();
                var snapshot = _session.Runtime.ExportPureStateSnapshotTransient(
                    worldId: 0,
                    isFullBaseline: isFullBaseline,
                    settings: _presentationPureStateSettings,
                    baselineFrame: _presentationPureStateBaselineFrame,
                    baselineHash: _presentationPureStateBaselineHash,
                    computeStateHash: false);
                if (isFullBaseline)
                {
                    _hasPresentationPureStateBaseline = true;
                    _presentationPureStateBaselineFrame = snapshot.Frame;
                    _presentationPureStateBaselineHash = snapshot.StateHash;
                }

                _session.Presentation.ApplyPureStateSnapshot(in snapshot);
                return;
            }

            var fullSnapshot = _session.Runtime.GetSnapshotTransient();
            _session.Presentation.ApplyLocalAuthoritativeSnapshot(in fullSnapshot);
        }

        private void RenderLatest()
        {
            if (_session == null)
            {
                return;
            }

            var frame = new ShooterHostPresentationFrame(
                _session.Presentation.ViewModel.Current,
                _session.AuthoritativePresentation?.ViewModel.Current ?? ShooterSnapshotViewBatch.Empty,
                _session.HasAuthoritativeWorld && _session.AuthoritativePresentation != null,
                _options.ControlledPlayerId,
                _options.WorldScale,
                _session.CarrierNetworkStats,
                _session.LastCarrierSnapshotApplyResult,
                _session.LastCarrierTimeAnchor,
                LastLocalTimeAnchor,
                _session.LagCompensationTelemetry,
                _session.LastLagCompensationEvaluation,
                default,
                ShooterCrossLayerDiagnostics.From(
                    _session.FrameworkSnapshotPipelineDiagnostics,
                    _session.LastCarrierSnapshotApplyResult,
                    default,
                    _session.Presentation.NeedsPureStateFullBaselineResync,
                    _session.Presentation.LastPureStateAppliedFrame,
                    _session.Presentation.LastPureStateResyncFrame),
                _session.Presentation.LastPureStateSyncDiagnostics,
                _session.Presentation.NeedsPureStateFullBaselineResync,
                _session.Presentation.LastPureStateResyncReason,
                _session.Presentation.LastPureStateAppliedFrame,
                _session.Presentation.LastPureStateAppliedStateHash,
                _session.Presentation.LastPureStateResyncFrame,
                _session.Presentation.LastPureStateResyncStateHash);
            _viewSink.Render(in frame);
            _renderCount++;
        }

        private bool ShouldPublishPresentationSnapshot()
        {
            return _stepCount <= 1 || (_stepCount % Math.Max(1, _presentationSnapshotIntervalTicks)) == 0;
        }

        private bool ShouldPublishPresentationPureStateBaseline()
        {
            if (!_hasPresentationPureStateBaseline)
            {
                return true;
            }

            var baselineInterval = Math.Max(1, _presentationPureStateSettings.BaselineIntervalFrames);
            return _stepCount > 0 && _stepCount % baselineInterval == 0;
        }

        private static int DeterminePresentationSnapshotIntervalTicks(ShooterPlayModeSessionOptions options)
        {
            var activeEnemies = options.GameplayScenario.BattleFlow.MaxActiveEnemies;
            if (activeEnemies >= ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget)
            {
                return Math.Max(1, options.TickRate / 20);
            }

            if (activeEnemies >= ShooterPlayModeSessionOptions.PlayModeMediumEnemyBudget)
            {
                return Math.Max(1, options.TickRate / 30);
            }

            return 1;
        }

        private static bool ShouldUsePureStatePresentationSnapshots(ShooterPlayModeSessionOptions options)
        {
            return options.GameplayScenario.BattleFlow.MaxActiveEnemies >= ShooterPlayModeSessionOptions.PlayModeHighDensityEnemyBudget;
        }

        private static ShooterPureStateSyncSettings CreatePresentationPureStateSettings(ShooterPlayModeSessionOptions options)
        {
            var defaults = ShooterPureStateSyncSettings.Default;
            var maxEntities = Math.Max(defaults.MaxEntityCount, options.GameplayScenario.BattleFlow.MaxActiveEnemies + options.PlayerCount + 1024);
            return new ShooterPureStateSyncSettings(
                maxEntities,
                defaults.ActiveSyncBudget,
                Math.Max(1, options.TickRate * HighDensityPresentationBaselineIntervalSeconds),
                1,
                Math.Max(1, options.TickRate / 2),
                1);
        }

        private static ShooterPlayModeSessionOptions AlignWithGameplayScenario(ShooterPlayModeSessionOptions options)
        {
            var scenario = options.GameplayScenario;
            var tickRate = Math.Max(1, (int)Math.Round(1f / scenario.TickDeltaTime));
            var playerCount = Math.Max(options.PlayerCount, scenario.ShooterCount);

            return new ShooterPlayModeSessionOptions(
                options.SyncModel,
                tickRate,
                playerCount,
                options.RandomSeed,
                Math.Min(options.ControlledPlayerId, playerCount),
                options.EnableAuthoritativeWorld,
                options.LatencyMs,
                options.JitterMs,
                options.PacketLossRate,
                options.ReorderRate,
                options.BandwidthKbps,
                options.WorldScale,
                options.NetworkName,
                options.SyncTemplateId,
                scenario).Normalized();
        }

        private static NetworkConditionProfile CreateProfile(ShooterPlayModeSessionOptions options)
        {
            return new NetworkConditionProfile(
                options.LatencyMs,
                options.JitterMs,
                options.PacketLossRate,
                options.ReorderRate,
                options.BandwidthKbps);
        }
    }
}


