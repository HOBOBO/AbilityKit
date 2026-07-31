using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(70)]
    [WorldService(typeof(MobaStateHashSnapshotService))]
    public sealed class MobaStateHashSnapshotService : IService, IMobaSnapshotEmitter
    {
        private const int DefaultIntervalFrames = 10;
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _registry;
        private readonly MobaAuthoritativeStateHashCalculator _hashCalculator =
            new MobaAuthoritativeStateHashCalculator();

        private FrameIndex _lastFrame;

        public int IntervalFrames { get; set; } = DefaultIntervalFrames;

        public MobaStateHashSnapshotService(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }

            var interval = IntervalFrames;
            if (interval <= 0) interval = DefaultIntervalFrames;

            if ((frame.Value % interval) != 0)
            {
                snapshot = default;
                return false;
            }

            _lastFrame = frame;

            var hash = _hashCalculator.Compute(_phase.InGame, _registry);
            var payload = MobaStateHashSnapshotCodec.Serialize(frame.Value, hash);
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.StateHash, payload);
            return true;
        }

        public void Dispose()
        {
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
