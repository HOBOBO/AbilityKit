using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Battle.Testing
{
    public interface IMobaPredictionScenarioBackend
    {
        string Id { get; }
        IMobaPredictionFrameRoute CreateRoute(
            IClientPredictionReconcileTarget target, WorldId worldId, int tickRate);
    }

    public interface IMobaPredictionFrameRoute : IDisposable
    {
        void Feed(FramePacket packet);
    }

    public sealed class HeadlessMobaPredictionScenarioBackend : IMobaPredictionScenarioBackend
    {
        public string Id => MobaPredictionBackendIds.Headless;

        public IMobaPredictionFrameRoute CreateRoute(
            IClientPredictionReconcileTarget target, WorldId worldId, int tickRate)
            => new Route(target);

        private sealed class Route : IMobaPredictionFrameRoute
        {
            private readonly IClientPredictionReconcileTarget _target;
            private bool _disposed;

            public Route(IClientPredictionReconcileTarget target)
                => _target = target ?? throw new ArgumentNullException(nameof(target));

            public void Feed(FramePacket packet)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Route));
                if (!packet.Snapshot.HasValue ||
                    packet.Snapshot.Value.OpCode != MobaOpCodes.Snapshot.StateHash) return;
                var payload = MobaStateHashSnapshotCodec.Deserialize(packet.Snapshot.Value.Payload);
                _target.OnAuthoritativeStateHash(
                    packet.WorldId, new FrameIndex(payload.Frame), new WorldStateHash(payload.Hash));
            }

            public void Dispose() => _disposed = true;
        }
    }
}
