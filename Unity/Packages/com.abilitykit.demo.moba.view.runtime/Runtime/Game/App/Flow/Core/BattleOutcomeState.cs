using System;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleOutcomeState
    {
        private int _gameplayId;
        private string _matchId = string.Empty;
        private int _lastFrame = -1;

        public bool HasAuthoritativeResult { get; private set; }
        public GameplayMatchData Result { get; private set; }

        public void SetIdentity(int gameplayId, string matchId)
        {
            if (gameplayId <= 0) return;
            if (_gameplayId != 0 && _gameplayId != gameplayId)
                throw new InvalidOperationException("Battle outcome gameplay identity changed.");
            if (!string.IsNullOrEmpty(_matchId) && !string.IsNullOrEmpty(matchId) &&
                !string.Equals(_matchId, matchId, StringComparison.Ordinal))
                throw new InvalidOperationException("Battle outcome match identity changed.");
            _gameplayId = gameplayId;
            if (!string.IsNullOrEmpty(matchId)) _matchId = matchId;
        }

        public bool Apply(in GameplayMatchData match)
        {
            if (match.GameplayId <= 0 || match.SnapshotFrame <= _lastFrame ||
                (_gameplayId != 0 && _gameplayId != match.GameplayId) ||
                (!string.IsNullOrEmpty(_matchId) &&
                 !string.Equals(_matchId, match.MatchId, StringComparison.Ordinal))) return false;
            SetIdentity(match.GameplayId, match.MatchId);
            _lastFrame = match.SnapshotFrame;
            if (match.Phase != GameplayClockPhase.Ended) return false;
            Result = match;
            HasAuthoritativeResult = true;
            return true;
        }

        public void Reset()
        {
            _gameplayId = 0;
            _matchId = string.Empty;
            _lastFrame = -1;
            HasAuthoritativeResult = false;
            Result = default;
        }
    }
}
