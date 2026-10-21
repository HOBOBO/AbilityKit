using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(90)]
    [WorldService(typeof(MobaGameplayClockSnapshotService))]
    public sealed class MobaGameplayClockSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaGameplayService _gameplay;
        private int _lastFrame = -1;
        private int _lastPublishedFrame = -1;
        private bool _hasPublished;
        private GameplayClockData _lastPublished;

        public MobaGameplayClockSnapshotService(MobaGameplayService gameplay)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (frame.Value == _lastFrame) return false;

            var state = _gameplay.GetClockState();
            var clock = new GameplayClockData(state.GameplayId, (GameplayClockPhase)state.Phase,
                state.DurationMilliseconds, state.RemainingMilliseconds,
                _gameplay.MatchId, frame.Value, state.ElapsedMilliseconds);
            if (!_hasPublished && state.Phase == MobaGameplayPhase.NotStarted) return false;
            if (_hasPublished && clock.GameplayId == _lastPublished.GameplayId &&
                clock.MatchId == _lastPublished.MatchId &&
                clock.Phase == _lastPublished.Phase &&
                clock.DurationMilliseconds == _lastPublished.DurationMilliseconds &&
                DisplaySeconds(clock) == DisplaySeconds(_lastPublished) &&
                frame.Value >= _lastPublishedFrame && frame.Value - _lastPublishedFrame < 30)
                return false;

            snapshot = new WorldStateSnapshot(GameplayClockSnapshotCodec.OpCode,
                GameplayClockSnapshotCodec.Encode(in clock));
            _lastPublished = clock;
            _lastPublishedFrame = frame.Value;
            _hasPublished = true;
            _lastFrame = frame.Value;
            return true;
        }

        public void Dispose()
        {
            _lastFrame = -1;
            _lastPublishedFrame = -1;
            _hasPublished = false;
            _lastPublished = default;
        }

        private static int DisplaySeconds(GameplayClockData clock) => clock.DurationMilliseconds > 0
            ? clock.RemainingMilliseconds / 1000 + (clock.RemainingMilliseconds % 1000 == 0 ? 0 : 1)
            : clock.ElapsedMilliseconds / 1000;
    }
}
