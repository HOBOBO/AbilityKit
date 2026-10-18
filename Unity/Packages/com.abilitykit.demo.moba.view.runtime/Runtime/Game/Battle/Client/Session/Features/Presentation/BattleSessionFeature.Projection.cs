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

        public bool ShowConfirmedView(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors,
            Camera camera = null)
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
                _confirmedProjectionSource, worldOffset, capabilities, camera);
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
            BattleProjectionViewCapabilities capabilities, Camera camera)
        {
            if ((HasActiveBattleView() || _runtime.Presentation.ProjectionViewCount > 0) &&
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
