using System;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleGameplayPresentationState
    {
        private int _gameplayId;
        private string _matchId = string.Empty;
        private int _lastClockFrame = -1;
        private int _lastResultFrame = -1;
        private MobaPlayerLoadout[] _players = Array.Empty<MobaPlayerLoadout>();
        private readonly TeamDeathmatchPresentationState _deathmatch = new TeamDeathmatchPresentationState();

        public int GameplayId => _gameplayId;
        public string HudModuleId { get; private set; } = string.Empty;
        public string ProjectionModuleId { get; private set; } = string.Empty;
        public string SettlementModuleId { get; private set; } = string.Empty;
        public string MatchId => _matchId;
        public MobaPlayerLoadout[] Players => _players;
        public string LocalPlayerId { get; private set; } = string.Empty;
        public int TickRate { get; private set; } = 30;
        public TeamDeathmatchPresentationState Deathmatch => _deathmatch;
        public bool HasMatch => _deathmatch.HasMatch;
        public GameplayMatchData Match => _deathmatch.Match;
        public bool HasClock { get; private set; }
        public GameplayClockData Clock { get; private set; }
        public GameplayRespawnData[] Respawns => _deathmatch.Respawns;
        public bool HasAuthoritativeResult { get; private set; }
        public bool HasEndedMatch => HasAuthoritativeResult;

        public event Action<GameplayMatchData> MatchChanged
        {
            add => _deathmatch.MatchChanged += value;
            remove => _deathmatch.MatchChanged -= value;
        }
        public event Action<GameplayClockData> ClockChanged;
        public event Action<GameplayRespawnData[]> RespawnsChanged
        {
            add => _deathmatch.RespawnsChanged += value;
            remove => _deathmatch.RespawnsChanged -= value;
        }
        public event Action Cleared;

        public void Reset()
        {
            _gameplayId = 0;
            HudModuleId = string.Empty;
            ProjectionModuleId = string.Empty;
            SettlementModuleId = string.Empty;
            _matchId = string.Empty;
            _lastClockFrame = -1;
            _lastResultFrame = -1;
            _players = Array.Empty<MobaPlayerLoadout>();
            LocalPlayerId = string.Empty;
            TickRate = 30;
            _deathmatch.Reset();
            HasAuthoritativeResult = false;
            HasClock = false;
            Clock = default;
            Cleared?.Invoke();
        }

        public void SetLaunch(int gameplayId, MobaPlayerLoadout[] players, string localPlayerId,
            int tickRate = 30, string matchId = null)
        {
            if (gameplayId <= 0) throw new ArgumentOutOfRangeException(nameof(gameplayId));
            if (_gameplayId != 0 && _gameplayId != gameplayId)
                throw new InvalidOperationException("Gameplay identity changed within a battle.");
            if (!string.IsNullOrEmpty(_matchId) && !string.IsNullOrEmpty(matchId) &&
                !string.Equals(_matchId, matchId, StringComparison.Ordinal))
                throw new InvalidOperationException("Match identity changed within a battle.");
            _gameplayId = gameplayId;
            if (!string.IsNullOrEmpty(matchId)) _matchId = matchId;
            _players = players ?? Array.Empty<MobaPlayerLoadout>();
            LocalPlayerId = localPlayerId ?? string.Empty;
            TickRate = tickRate > 0 ? tickRate : 30;
        }

        public void SelectModules(int gameplayId, string hudModuleId, string projectionModuleId,
            string settlementModuleId)
        {
            if (gameplayId <= 0 || (_gameplayId > 0 && _gameplayId != gameplayId))
                throw new InvalidOperationException("Presentation module gameplay identity does not match the battle.");
            if (string.IsNullOrWhiteSpace(hudModuleId) ||
                string.IsNullOrWhiteSpace(projectionModuleId) ||
                string.IsNullOrWhiteSpace(settlementModuleId))
                throw new ArgumentException("Presentation module ids are required.");
            if (!string.IsNullOrEmpty(ProjectionModuleId) &&
                (HudModuleId != hudModuleId || ProjectionModuleId != projectionModuleId ||
                 SettlementModuleId != settlementModuleId))
                throw new InvalidOperationException("Presentation modules changed within a battle.");
            _gameplayId = gameplayId;
            HudModuleId = hudModuleId;
            ProjectionModuleId = projectionModuleId;
            SettlementModuleId = settlementModuleId;
        }

        public void Apply(in GameplayMatchData match)
        {
            if (match.GameplayId <= 0) return;
            if (_gameplayId > 0 && match.GameplayId != _gameplayId) return;
            if (!AcceptIdentity(match.MatchId) || match.SnapshotFrame <= _deathmatch.LastMatchFrame) return;
            if (HasClock && Clock.Phase == GameplayClockPhase.Ended &&
                match.Phase != GameplayClockPhase.Ended && match.SnapshotFrame <= _lastClockFrame) return;
            if (_gameplayId == 0) _gameplayId = match.GameplayId;
            if (_deathmatch.Apply(in match) && match.Phase == GameplayClockPhase.Ended)
                TryApplyAuthoritativeResult(match.GameplayId, match.MatchId, match.SnapshotFrame);
        }

        public void Apply(in GameplayClockData clock)
        {
            if (clock.GameplayId <= 0) return;
            if (_gameplayId > 0 && clock.GameplayId != _gameplayId) return;
            if (!AcceptIdentity(clock.MatchId) || clock.SnapshotFrame <= _lastClockFrame) return;
            if (HasEndedMatch && clock.Phase != GameplayClockPhase.Ended &&
                clock.SnapshotFrame <= _deathmatch.LastMatchFrame) return;
            if (_gameplayId == 0) _gameplayId = clock.GameplayId;
            _lastClockFrame = clock.SnapshotFrame;
            Clock = clock;
            HasClock = true;
            ClockChanged?.Invoke(clock);
        }

        public void Apply(GameplayRespawnData[] entries)
        {
            _deathmatch.Apply(entries);
        }

        public bool TryApplyAuthoritativeResult(int gameplayId, string matchId, int snapshotFrame)
        {
            if (gameplayId <= 0 || (_gameplayId > 0 && gameplayId != _gameplayId) ||
                !AcceptIdentity(matchId) || snapshotFrame <= _lastResultFrame)
                return false;
            if (_gameplayId == 0) _gameplayId = gameplayId;
            _lastResultFrame = snapshotFrame;
            HasAuthoritativeResult = true;
            return true;
        }

        private bool AcceptIdentity(string matchId)
        {
            if (string.IsNullOrEmpty(_matchId))
            {
                if (!string.IsNullOrEmpty(matchId)) _matchId = matchId;
                return true;
            }
            return string.Equals(_matchId, matchId, StringComparison.Ordinal);
        }
    }

    public sealed class TeamDeathmatchPresentationState
    {
        public int LastMatchFrame { get; private set; } = -1;
        public bool HasMatch { get; private set; }
        public GameplayMatchData Match { get; private set; }
        public GameplayRespawnData[] Respawns { get; private set; } = Array.Empty<GameplayRespawnData>();
        public bool HasEndedMatch => HasMatch && Match.Phase == GameplayClockPhase.Ended;

        public event Action<GameplayMatchData> MatchChanged;
        public event Action<GameplayRespawnData[]> RespawnsChanged;

        public bool Apply(in GameplayMatchData match)
        {
            if (match.SnapshotFrame <= LastMatchFrame) return false;
            LastMatchFrame = match.SnapshotFrame;
            Match = match;
            HasMatch = true;
            MatchChanged?.Invoke(match);
            return true;
        }

        public void Apply(GameplayRespawnData[] entries)
        {
            Respawns = entries ?? Array.Empty<GameplayRespawnData>();
            RespawnsChanged?.Invoke(Respawns);
        }

        public void Reset()
        {
            LastMatchFrame = -1;
            HasMatch = false;
            Match = default;
            Respawns = Array.Empty<GameplayRespawnData>();
        }
    }
}
