using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbyStateService : IService
    {
        private readonly Dictionary<string, bool> _players = new Dictionary<string, bool>();
        private bool _started;
        private EnterMobaGameReq? _pendingEnterReq;
        private int _version;

        private int _minPlayers = 1;
        private int _maxPlayers;
        private MobaStartCondition _startCondition = MobaStartCondition.AllReady;
        private MobaRoomPhase _phase = MobaRoomPhase.Lobby;

        public void SetEnterGameReq(in EnterMobaGameReq req)
        {
            _pendingEnterReq = req;
            _version++;
        }

        public void OnPlayerJoined(PlayerId playerId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return;
            if (_players.ContainsKey(playerId.Value)) return;
            _players[playerId.Value] = false;
            _version++;
        }

        public void OnPlayerLeft(PlayerId playerId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return;
            if (_players.Remove(playerId.Value)) _version++;
        }

        public void Configure(int minPlayers, int maxPlayers, MobaStartCondition startCondition)
        {
            _minPlayers = minPlayers < 1 ? 1 : minPlayers;
            _maxPlayers = maxPlayers;
            _startCondition = startCondition;
            _version++;
        }

        public bool TryGetEnterGameReq(out EnterMobaGameReq req)
        {
            if (_pendingEnterReq.HasValue)
            {
                req = _pendingEnterReq.Value;
                return true;
            }

            req = default;
            return false;
        }

        public void SetReady(PlayerId playerId, bool ready)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return;

            // Ready only applies to joined players.
            if (!_players.TryGetValue(playerId.Value, out var old)) return;
            if (old == ready) return;
            _players[playerId.Value] = ready;
            _version++;
        }

        public bool IsReady(PlayerId playerId)
        {
            return !string.IsNullOrEmpty(playerId.Value) && _players.TryGetValue(playerId.Value, out var r) && r;
        }

        public bool HasAnyPlayer => _players.Count > 0;

        public int PlayerCount => _players.Count;

        public bool AllReady
        {
            get
            {
                if (_players.Count == 0) return false;
                foreach (var kv in _players)
                {
                    if (!kv.Value) return false;
                }
                return true;
            }
        }

        public bool Started => _started;

        public MobaRoomPhase Phase => _phase;

        public int MinPlayers => _minPlayers;
        public int MaxPlayers => _maxPlayers;
        public MobaStartCondition StartCondition => _startCondition;

        public int Version => _version;

        public PlayerReadyEntry[] GetPlayers()
        {
            if (_players.Count == 0) return Array.Empty<PlayerReadyEntry>();

            var arr = new PlayerReadyEntry[_players.Count];
            var i = 0;
            foreach (var kv in _players)
            {
                arr[i++] = new PlayerReadyEntry(new PlayerId(kv.Key), kv.Value);
            }
            return arr;
        }

        public bool TryMarkStarted()
        {
            if (_started) return false;
            _started = true;
            _phase = MobaRoomPhase.InGame;
            _version++;
            return true;
        }

        public void Reset()
        {
            _players.Clear();
            _started = false;
            _pendingEnterReq = null;
            _phase = MobaRoomPhase.Lobby;
            _version++;
        }

        public bool CanStartGame()
        {
            if (_started) return false;
            if (PlayerCount < _minPlayers) return false;
            if (_maxPlayers > 0 && PlayerCount > _maxPlayers) return false;

            if (_startCondition == MobaStartCondition.AllReady)
            {
                return AllReady;
            }

            if (_startCondition == MobaStartCondition.Full)
            {
                return _maxPlayers > 0 && PlayerCount == _maxPlayers;
            }

            if (_startCondition == MobaStartCondition.FullAndAllReady)
            {
                return _maxPlayers > 0 && PlayerCount == _maxPlayers && AllReady;
            }

            return false;
        }

        public void Dispose()
        {
            _players.Clear();
            _pendingEnterReq = null;
        }
    }

    public enum MobaRoomPhase
    {
        Lobby = 0,
        InGame = 1,
    }

    public enum MobaStartCondition
    {
        AllReady = 0,
        Full = 1,
        FullAndAllReady = 2,
    }
}
