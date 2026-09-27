using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Projection;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        public int PredictionViewCount => _runtime.Presentation.PredictionViewCount;
        public int ProjectionViewCount => _runtime.Presentation.ProjectionViewCount;

        public bool TryGetProjectionView(string instanceId, out BattleProjectionViewInfo info) =>
            _runtime.Presentation.TryGetProjectionView(instanceId, out info);

        public void GetProjectionViews(List<BattleProjectionViewInfo> buffer) =>
            _runtime.Presentation.GetProjectionViews(buffer);

        public void RebindProjectionViews() => _runtime.Presentation.RebindProjectionViews();

        public bool ShowProjectionView(string instanceId, IBattleProjectionViewSource source,
            Vector3 worldOffset) => ShowProjectionView(instanceId, source, worldOffset,
                BattleProjectionViewCapabilities.Actors);

        public bool ShowProjectionView(string instanceId, IBattleProjectionViewSource source,
            Vector3 worldOffset, BattleProjectionViewCapabilities capabilities)
        {
            return _runtime.Presentation.AddProjectionView(instanceId,
                BattleProjectionViewRole.Auxiliary, source, _ctx, _flow,
                worldOffset, DestroyEntityTree, capabilities);
        }

        public bool HideProjectionView(string instanceId) =>
            _runtime.Presentation.RemoveProjectionView(instanceId);

        public bool ShowPredictionView(string instanceId, Vector3 worldOffset) =>
            ShowPredictionView(instanceId, worldOffset, BattleProjectionViewCapabilities.Actors);

        public bool ShowPredictionView(string instanceId, Vector3 worldOffset,
            BattleProjectionViewCapabilities capabilities)
        {
            if (!_plan.Authority.EnableClientPrediction ||
                _runtime.Simulation.RemoteDriven.World == null ||
                _runtime.Simulation.RemoteDriven.Capabilities.ProjectionProducer == null ||
                _ctx == null) return false;
            return _runtime.Presentation.AddProjectionView(
                instanceId, BattleProjectionViewRole.Prediction,
                new RemoteDrivenProjectionViewSource(_runtime.Simulation, _ctx.RuntimeWorldId),
                _ctx, _flow, worldOffset, DestroyEntityTree, capabilities);
        }

        public bool HidePredictionView(string instanceId) =>
            HideProjectionView(instanceId);

        private sealed class RemoteDrivenProjectionViewSource :
            IBattleProjectionViewSource, IBattleProjectionViewEpochSource
        {
            private readonly BattleSimulationRuntime _simulation;
            private object _lastWorld;
            private long _epoch;

            public RemoteDrivenProjectionViewSource(BattleSimulationRuntime simulation, WorldId worldId)
            {
                _simulation = simulation;
                WorldId = worldId;
            }

            public WorldId WorldId { get; }
            public long ProjectionEpoch => _epoch;

            public bool TryGetProjection(out IActorProjectionProducer producer, out int frame)
            {
                frame = _simulation.RemoteDrivenLastTickedFrame;
                var world = _simulation.RemoteDriven.World;
                if (!ReferenceEquals(_lastWorld, world))
                {
                    _lastWorld = world;
                    _epoch++;
                }
                producer = world != null
                    ? _simulation.RemoteDriven.Capabilities.ProjectionProducer
                    : null;
                return producer != null;
            }
        }

        private void TickRemoteDrivenLocalSim(float deltaTime)
        {
            _runtime.Simulation.TickRemoteDriven(
                _plan,
                _ctx,
                _worldCatchUp,
                _snapshots,
                GetFixedDeltaSeconds(),
                _lastServerAckFrame);
        }
    }
}
