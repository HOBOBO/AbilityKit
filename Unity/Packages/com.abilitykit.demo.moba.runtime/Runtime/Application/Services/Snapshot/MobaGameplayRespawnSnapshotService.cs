using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(92)]
    [WorldService(typeof(MobaGameplayRespawnSnapshotService))]
    public sealed class MobaGameplayRespawnSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaGameplayRespawnService _respawns;
        private int _lastFrame = -1;
        private int _lastPublishedFrame = -1;
        private GameplayRespawnData[] _lastPublished = Array.Empty<GameplayRespawnData>();

        public MobaGameplayRespawnSnapshotService(MobaGameplayRespawnService respawns)
        {
            _respawns = respawns ?? throw new ArgumentNullException(nameof(respawns));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (frame.Value == _lastFrame) return false;
            _lastFrame = frame.Value;

            var pending = _respawns.CapturePending();
            var current = new GameplayRespawnData[pending.Length];
            for (var i = 0; i < pending.Length; i++)
                current[i] = new GameplayRespawnData(pending[i].ActorId, pending[i].RemainingMilliseconds);
            if (current.Length == 0 && _lastPublished.Length == 0) return false;
            if (SameDisplayedState(current, _lastPublished) && frame.Value >= _lastPublishedFrame &&
                frame.Value - _lastPublishedFrame < 30) return false;

            snapshot = new WorldStateSnapshot(GameplayRespawnSnapshotCodec.OpCode,
                GameplayRespawnSnapshotCodec.Encode(current));
            _lastPublished = current;
            _lastPublishedFrame = frame.Value;
            return true;
        }

        private static bool SameDisplayedState(GameplayRespawnData[] left, GameplayRespawnData[] right)
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i].ActorId != right[i].ActorId ||
                    DisplaySeconds(left[i].RemainingMilliseconds) != DisplaySeconds(right[i].RemainingMilliseconds))
                    return false;
            }
            return true;
        }

        private static int DisplaySeconds(int milliseconds) =>
            milliseconds / 1000 + (milliseconds % 1000 == 0 ? 0 : 1);

        public void Dispose()
        {
            _lastFrame = -1;
            _lastPublishedFrame = -1;
            _lastPublished = Array.Empty<GameplayRespawnData>();
        }
    }
}
