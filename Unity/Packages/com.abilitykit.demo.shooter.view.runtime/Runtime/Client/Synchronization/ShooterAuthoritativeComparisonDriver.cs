#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 负责验收会话中的权威世界推进、Carrier 快照发布和 LagComp 历史采集。
    /// <see cref="ShooterAcceptanceSession"/> 只保留会话门面职责，具体的权威侧编排集中在这里。
    /// </summary>
    internal sealed class ShooterAuthoritativeComparisonDriver : IDisposable
    {
        private readonly IShooterClientSyncController _controller;
        private readonly ShooterBattleRuntimePort _authoritativeWorld;
        private readonly ShooterBattleDriverHost _driverHost;
        private readonly ShooterPresentationFacade? _authoritativePresentation;
        private readonly ShooterLagCompensationService _lagCompensation = new ShooterLagCompensationService();

        private readonly Queue<PendingAuthoritativeInput> _pendingInputs = new Queue<PendingAuthoritativeInput>();
        private readonly Random _inputRandom;
        private readonly ShooterAuthoritySnapshotPublishOptions _publishOptions;
        private ShooterCarrierNetworkLink _carrierNetworkLink;
        private NetworkConditionProfile _networkProfile;
        private SyncTimeAnchor _lastCarrierTimeAnchor;
        private double _networkElapsedSeconds;
        private int _lastDeliveredInputCount;
        private int _lastPureStateBaselineFrame;
        private uint _lastPureStateBaselineHash;

        public ShooterAuthoritativeComparisonDriver(
            IShooterClientSyncController controller,
            ShooterBattleRuntimePort authoritativeWorld,
            ShooterPresentationFacade? authoritativePresentation,
            NetworkConditionProfile networkProfile,
            ShooterAuthoritySnapshotPublishOptions publishOptions,
            int networkSeed)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _authoritativeWorld = authoritativeWorld ?? throw new ArgumentNullException(nameof(authoritativeWorld));
            _driverHost = new ShooterBattleDriverHost(_authoritativeWorld);
            _driverHost.Start();
            _authoritativePresentation = authoritativePresentation;
            _networkProfile = networkProfile;
            _publishOptions = publishOptions;
            _inputRandom = new Random(networkSeed);
            _carrierNetworkLink = new ShooterCarrierNetworkLink(_controller, networkProfile, networkSeed);
        }

        public NetworkConditioningStats Stats => _carrierNetworkLink.Stats;

        public ShooterSnapshotApplyResult LastApplyResult => _carrierNetworkLink.LastApplyResult;

        public SyncTimeAnchor LastCarrierTimeAnchor => _lastCarrierTimeAnchor;

        public ShooterLagCompensationTelemetry Telemetry => _lagCompensation.Telemetry;

        public ShooterLagCompensationEvaluation? LastLagCompensationEvaluation => _lagCompensation.LastEvaluation;

        public int LastDeliveredInputCount => _lastDeliveredInputCount;

        public bool TryEvaluateShot(in ShooterLagCompensationShot shot, out ShooterLagCompensationEvaluation evaluation)
        {
            _lagCompensation.TryEvaluateShot(in shot, out _);
            if (_lagCompensation.LastEvaluation.HasValue)
            {
                evaluation = _lagCompensation.LastEvaluation.Value;
                return evaluation.Accepted;
            }

            evaluation = default;
            return false;
        }
 
        public void ApplyNetwork(NetworkConditionProfile profile)
        {
            _carrierNetworkLink.Dispose();
            _networkProfile = profile;
            _pendingInputs.Clear();
            _lastDeliveredInputCount = 0;
            _lastPureStateBaselineFrame = 0;
            _lastPureStateBaselineHash = 0u;
            _carrierNetworkLink = new ShooterCarrierNetworkLink(_controller, profile);
            _lagCompensation.Clear();
            _lastCarrierTimeAnchor = default;
            _networkElapsedSeconds = 0d;
        }

        public void Dispose()
        {
            _carrierNetworkLink.Dispose();
            _pendingInputs.Clear();
            _driverHost.Stop();
        }

        public void EnqueueInput(int commandFrame, in ShooterPlayerCommand command)
        {
            if (_networkProfile.PacketLossRate > 0d && _inputRandom.NextDouble() < _networkProfile.PacketLossRate)
            {
                return;
            }

            _pendingInputs.Enqueue(new PendingAuthoritativeInput(_networkElapsedSeconds, commandFrame, command));
        }

        public void Advance(int stepCount, float deltaSeconds)
        {
            if (stepCount <= 0)
            {
                return;
            }

            for (var i = 0; i < stepCount; i++)
            {
                _lastDeliveredInputCount = DeliverDueInputs();
                _driverHost.AdvanceFrame(deltaSeconds);
                _lagCompensation.RecordFrame(_authoritativeWorld);
                _networkElapsedSeconds = _driverHost.LogicTimeSeconds;
                var authorityFrame = _driverHost.CurrentFrame;
                var anchor = SyncTimeAnchor
                    .FromLocalFrame(authorityFrame, authorityFrame, _networkElapsedSeconds)
                    .WithAuthoritativeFrame(authorityFrame);
                PublishSnapshot(in anchor);
            }

            if (_authoritativePresentation != null)
            {
                var authoritySnapshot = _authoritativeWorld.GetSnapshotTransient();
                _authoritativePresentation.ApplyLocalAuthoritativeSnapshot(in authoritySnapshot);
            }
        }

        private int DeliverDueInputs()
        {
            var delivered = 0;
            while (_pendingInputs.Count > 0 && _pendingInputs.Peek().DeliverAtSeconds <= _networkElapsedSeconds)
            {
                var pending = _pendingInputs.Dequeue();
                delivered += _driverHost.SubmitCommand(pending.CommandFrame, in pending.Command);
            }

            return delivered;
        }

        private void PublishSnapshot(in SyncTimeAnchor anchor)
        {
            _lastCarrierTimeAnchor = anchor;
            var clockMs = (long)Math.Round(anchor.ElapsedSeconds * 1000d);

            if (_publishOptions.UsesPureStatePayload)
            {
                PublishPureStateSnapshot(in anchor);
            }
            else
            {
                var packed = _authoritativeWorld.ExportPackedSnapshot(worldId: 1UL, isFullSnapshot: true, authorityOverride: true);
                _carrierNetworkLink.PublishSnapshot(in packed, anchor.ElapsedSeconds);
            }

            _carrierNetworkLink.Advance(clockMs);
        }

        private void PublishPureStateSnapshot(in SyncTimeAnchor anchor)
        {
            var sendPolicy = _publishOptions.SendPolicy;
            var snapshotInterval = Math.Max(1, sendPolicy.SnapshotIntervalFrames);
            var currentFrame = _authoritativeWorld.CurrentFrame;
            var isFullBaseline = ShouldPublishFullBaseline(currentFrame, sendPolicy.KeyFrameIntervalFrames);
            if (!isFullBaseline && currentFrame % snapshotInterval != 0)
            {
                return;
            }

            var settings = sendPolicy.ToPureStateSettings();
            var interestScope = CreateInterestScope(sendPolicy);
            var pureState = _authoritativeWorld.ExportPureStateSnapshotTransient(
                worldId: 1UL,
                isFullBaseline: isFullBaseline,
                settings: settings,
                baselineFrame: _lastPureStateBaselineFrame,
                baselineHash: _lastPureStateBaselineHash,
                interestScope: interestScope);

            if (isFullBaseline)
            {
                _lastPureStateBaselineFrame = pureState.Frame;
                _lastPureStateBaselineHash = pureState.StateHash;
            }

            _carrierNetworkLink.PublishSnapshot(in pureState, anchor.ElapsedSeconds);
        }

        private bool ShouldPublishFullBaseline(int currentFrame, int keyFrameIntervalFrames)
        {
            if (_lastPureStateBaselineFrame <= 0 || _lastPureStateBaselineHash == 0u)
            {
                return true;
            }

            return keyFrameIntervalFrames > 0 && currentFrame - _lastPureStateBaselineFrame >= keyFrameIntervalFrames;
        }

        private ShooterPureStateInterestScope? CreateInterestScope(ShooterSyncTemplateSendPolicy sendPolicy)
        {
            if (!_publishOptions.UsesAoiScope)
            {
                return null;
            }

            var players = _authoritativeWorld.GetPlayerSnapshotsTransient();
            if (players.Length > 0)
            {
                var observer = players[0];
                return new ShooterPureStateInterestScope(
                    observer.PlayerId,
                    observer.X,
                    observer.Y,
                    sendPolicy.AoiRadius,
                    sendPolicy.ActiveEntityBudget);
            }

            return new ShooterPureStateInterestScope(
                observerPlayerId: 0,
                centerX: 0f,
                centerY: 0f,
                radius: sendPolicy.AoiRadius,
                maxEntities: sendPolicy.ActiveEntityBudget);
        }

        private readonly struct PendingAuthoritativeInput
        {
            public readonly double DeliverAtSeconds;
            public readonly int CommandFrame;
            public readonly ShooterPlayerCommand Command;

            public PendingAuthoritativeInput(double deliverAtSeconds, int commandFrame, in ShooterPlayerCommand command)
            {
                DeliverAtSeconds = deliverAtSeconds;
                CommandFrame = commandFrame;
                Command = command;
            }
        }
    }
}
