using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Projection;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private BattleWorldProjectionViewSource _confirmedProjectionSource;
        private BattleWorldProjectionViewSource _spectatorProjectionSource;
        private BattleWorldProjectionViewSource _replayProjectionSource;
        private string _spectatorPresentationInstanceId;
        private string _replayPresentationInstanceId;

        private const string FrameSyncPredictionComparisonViewId = "FrameSyncComparison:Prediction";
        private const string FrameSyncConfirmedComparisonViewId = "FrameSyncComparison:Confirmed";

        public bool FrameSyncComparisonViewsCreated =>
            TryGetProjectionView(FrameSyncPredictionComparisonViewId, out _) &&
            TryGetProjectionView(FrameSyncConfirmedComparisonViewId, out _);

        private void ShowFrameSyncComparisonViews()
        {
            if (_plan.Sync.SyncMode != BattleSyncMode.Lockstep ||
                !_plan.Authority.EnableClientPrediction ||
                !_plan.Authority.EnableConfirmedAuthorityWorld ||
                _runtime.Simulation.RemoteDriven.World == null ||
                _runtime.Simulation.Confirmed.World == null ||
                _ctx == null || _flow == null)
                return;

            var scope = _plan.World.WorldId;
            BattleFlowDebugProvider.PublishFrameSyncComparisonViews(scope, false);

            var hasPrediction = TryGetProjectionView(
                FrameSyncPredictionComparisonViewId, out _);
            var hasConfirmed = TryGetProjectionView(
                FrameSyncConfirmedComparisonViewId, out _);
            if (hasPrediction && hasConfirmed)
            {
                BattleFlowDebugProvider.PublishFrameSyncComparisonViews(scope, true);
                return;
            }
            if (hasPrediction)
                _runtime.Presentation.RemoveProjectionView(FrameSyncPredictionComparisonViewId);
            if (hasConfirmed)
                _runtime.Presentation.RemoveProjectionView(FrameSyncConfirmedComparisonViewId);

            var predictionShown = false;
            try
            {
                predictionShown = ShowPredictionViewCore(
                    FrameSyncPredictionComparisonViewId, Vector3.zero, allowOverlap: true);
                if (!predictionShown || !ShowConfirmedViewCore(
                        FrameSyncConfirmedComparisonViewId, Vector3.zero, allowOverlap: true))
                    throw new InvalidOperationException(
                        "Unable to create both frame-sync comparison views.");
                BattleFlowDebugProvider.PublishFrameSyncComparisonViews(scope, true);
            }
            catch (Exception exception)
            {
                _runtime.Presentation.RemoveProjectionView(FrameSyncConfirmedComparisonViewId);
                if (predictionShown)
                    _runtime.Presentation.RemoveProjectionView(FrameSyncPredictionComparisonViewId);
                AbilityKit.Core.Logging.Log.Warning(
                    "[BattleSessionFeature] Frame-sync comparison views were not created: " + exception.Message);
            }
        }

        public bool ShowConfirmedView(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors,
            Camera camera = null)
        {
            if (!_plan.Authority.EnableConfirmedAuthorityWorld ||
                _runtime.Simulation.Confirmed.World == null) return false;

            return ShowConfirmedViewCore(instanceId, worldOffset, capabilities, camera,
                allowOverlap: false);
        }

        private bool ShowConfirmedViewCore(string instanceId, Vector3 worldOffset,
            bool allowOverlap) => ShowConfirmedViewCore(instanceId, worldOffset,
                BattleProjectionViewCapabilities.Actors, null, allowOverlap);

        private bool ShowConfirmedViewCore(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities, Camera camera, bool allowOverlap)
        {
            if (!_plan.Authority.EnableConfirmedAuthorityWorld ||
                _runtime.Simulation.Confirmed.World == null) return false;

            var worldId = ConfirmedAuthorityWorldId.Create(_plan);
            if (_confirmedProjectionSource == null ||
                !_confirmedProjectionSource.WorldId.Equals(worldId))
                _confirmedProjectionSource = new BattleWorldProjectionViewSource(
                    worldId, () => _runtime.Simulation.Confirmed.World,
                    () => _runtime.Simulation.ConfirmedLastTickedFrame);
            return ShowWorldView(instanceId, BattleProjectionViewRole.Confirmed,
                _confirmedProjectionSource, worldOffset, capabilities, camera, allowOverlap);
        }

        public bool ShowSpectatorView(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors,
            Camera camera = null)
        {
            var world = _runtime.Spectator.World;
            if (!_runtime.Spectator.IsSpectating || world == null ||
                _spectatorPresentationInstanceId != null) return false;

            _spectatorProjectionSource = new BattleWorldProjectionViewSource(
                world.Id, () => _runtime.Spectator.World,
                () => _runtime.Spectator.Driver?.CurrentFrame ?? 0);
            if (!ShowWorldView(instanceId, BattleProjectionViewRole.Spectator,
                    _spectatorProjectionSource, worldOffset, capabilities, camera))
            {
                _spectatorProjectionSource = null;
                return false;
            }

            _spectatorPresentationInstanceId = instanceId;
            return true;
        }

        public bool ShowReplayView(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors,
            Camera camera = null)
        {
            var world = _runtime.Replay.World;
            if (!_runtime.Replay.IsActive || world == null ||
                _replayPresentationInstanceId != null) return false;

            _replayProjectionSource = new BattleWorldProjectionViewSource(
                world.Id, () => _runtime.Replay.World,
                () => _runtime.Replay.CurrentFrame);
            if (!ShowWorldView(instanceId, BattleProjectionViewRole.Replay,
                    _replayProjectionSource, worldOffset, capabilities, camera))
            {
                _replayProjectionSource = null;
                return false;
            }

            _replayPresentationInstanceId = instanceId;
            return true;
        }

        public void HideSpectatorView()
        {
            var id = _spectatorPresentationInstanceId;
            _spectatorPresentationInstanceId = null;
            _spectatorProjectionSource = null;
            if (id != null) _runtime.Presentation.RemoveProjectionView(id);
        }

        public void HideReplayView()
        {
            var id = _replayPresentationInstanceId;
            _replayPresentationInstanceId = null;
            _replayProjectionSource = null;
            if (id != null) _runtime.Presentation.RemoveProjectionView(id);
        }

        private bool ShowWorldView(string instanceId, BattleProjectionViewRole role,
            IBattleProjectionViewSource source, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities, Camera camera,
            bool allowOverlap = false)
        {
            if (!allowOverlap && (HasActiveBattleView() || _runtime.Presentation.ProjectionViewCount > 0) &&
                worldOffset == Vector3.zero)
                return false;

            var permissions = BattleProjectionPermission.None;
            if (_plan.Authority.EnableClientPrediction)
                permissions |= BattleProjectionPermission.Prediction;
            if (_plan.Authority.EnableConfirmedAuthorityWorld)
                permissions |= BattleProjectionPermission.Confirmed;
            if (_runtime.Spectator.IsSpectating)
                permissions |= BattleProjectionPermission.Spectator;
            if (_runtime.Replay.IsActive)
                permissions |= BattleProjectionPermission.Replay;
            if (!new BattleProjectionSelectionPolicy(permissions).Allows(role)) return false;
            if (!source.TryGetProjection(out IActorProjectionProducer producer, out _) ||
                producer == null) return false;

            return _runtime.Presentation.AddProjectionView(instanceId, role,
                source, _ctx, _flow, worldOffset, DestroyEntityTree,
                capabilities, camera);
        }
    }
}
