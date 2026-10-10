using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(91)]
    [WorldService(typeof(MobaGameplayMatchSnapshotService))]
    public sealed class MobaGameplayMatchSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaGameplayService _gameplay;
        private readonly MobaGameplayVariableService _variables;
        private int _lastFrame = -1;
        private int _lastPublishedFrame = -1;
        private bool _hasPublished;
        private GameplayMatchData _lastPublished;

        public MobaGameplayMatchSnapshotService(MobaGameplayService gameplay,
            MobaGameplayVariableService variables)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (frame.Value == _lastFrame) return false;
            _lastFrame = frame.Value;

            var result = _gameplay.LastResult;
            var ended = _gameplay.Phase == MobaGameplayPhase.Ended;
            var state = new GameplayMatchData(_gameplay.CurrentGameplayId,
                (GameplayClockPhase)_gameplay.Phase,
                _variables.Get(GameplayScoreKeys.Team1),
                _variables.Get(GameplayScoreKeys.Team2),
                ended ? result.WinTeamId : 0,
                ended ? result.EndFrame : 0,
                ended ? result.Reason : null,
                _gameplay.MatchId, frame.Value);
            if (!_hasPublished && state.Phase == GameplayClockPhase.NotStarted) return false;
            if (_hasPublished && SameState(in state, in _lastPublished) &&
                frame.Value >= _lastPublishedFrame && frame.Value - _lastPublishedFrame < 30)
                return false;

            snapshot = new WorldStateSnapshot(GameplayMatchSnapshotCodec.OpCode,
                GameplayMatchSnapshotCodec.Encode(in state));
            _lastPublished = state;
            _lastPublishedFrame = frame.Value;
            _hasPublished = true;
            return true;
        }

        private static bool SameState(in GameplayMatchData left, in GameplayMatchData right)
        {
            return left.GameplayId == right.GameplayId && left.Phase == right.Phase &&
                string.Equals(left.MatchId, right.MatchId, StringComparison.Ordinal) &&
                left.Team1Score == right.Team1Score && left.Team2Score == right.Team2Score &&
                left.WinTeamId == right.WinTeamId && left.EndFrame == right.EndFrame &&
                string.Equals(left.EndReason, right.EndReason, StringComparison.Ordinal);
        }

        public void Dispose()
        {
            _lastFrame = -1;
            _lastPublishedFrame = -1;
            _hasPublished = false;
            _lastPublished = default;
        }
    }
}
