using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public sealed class MobaRoomState
    {
        private readonly Dictionary<string, PlayerSlot> _players = new Dictionary<string, PlayerSlot>();

        public int Revision { get; private set; }

        public string MatchId { get; private set; }
        public int MapId { get; private set; }
        public int RandomSeed { get; private set; }
        public int TickRate { get; private set; }
        public int InputDelayFrames { get; private set; }

        public int MinPlayers { get; private set; } = 1;
        public int MaxPlayers { get; private set; }

        public IReadOnlyDictionary<string, PlayerSlot> Players => _players;

        public MobaRoomState(string matchId, int mapId, int randomSeed, int tickRate, int inputDelayFrames)
        {
            MatchId = matchId;
            MapId = mapId;
            RandomSeed = randomSeed;
            TickRate = tickRate;
            InputDelayFrames = inputDelayFrames;
        }

        public void Configure(int minPlayers, int maxPlayers)
        {
            MinPlayers = minPlayers < 1 ? 1 : minPlayers;
            MaxPlayers = maxPlayers;
            Revision++;
        }

        public bool TryJoin(PlayerId playerId, int teamId = 0)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return false;
            if (_players.ContainsKey(playerId.Value)) return false;
            if (MaxPlayers > 0 && _players.Count >= MaxPlayers) return false;

            _players[playerId.Value] = new PlayerSlot(playerId, teamId);
            Revision++;
            return true;
        }

        public bool TryLeave(PlayerId playerId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return false;
            var ok = _players.Remove(playerId.Value);
            if (ok) Revision++;
            return ok;
        }

        public bool TrySetReady(PlayerId playerId, bool ready)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return false;
            if (!_players.TryGetValue(playerId.Value, out var slot)) return false;
            if (slot.Ready == ready) return false;

            slot = slot.WithReady(ready);
            _players[playerId.Value] = slot;
            Revision++;
            return true;
        }

        public bool TrySetSpawnPoint(PlayerId playerId, int spawnPointId)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return false;
            if (!_players.TryGetValue(playerId.Value, out var slot)) return false;
            if (slot.SpawnPointId == spawnPointId) return false;

            slot = slot.WithSpawnPoint(spawnPointId);
            _players[playerId.Value] = slot;
            Revision++;
            return true;
        }

        public bool TryGetPlayer(PlayerId playerId, out PlayerSlot slot)
        {
            if (string.IsNullOrEmpty(playerId.Value))
            {
                slot = default;
                return false;
            }
            return _players.TryGetValue(playerId.Value, out slot);
        }

        public bool TryPickHero(PlayerId playerId, int heroId, int attributeTemplateId = 0, int level = 1, int basicAttackSkillId = 0, int[] skillIds = null)
        {
            if (string.IsNullOrEmpty(playerId.Value)) return false;
            if (!_players.TryGetValue(playerId.Value, out var slot)) return false;

            slot = slot.WithHero(heroId, attributeTemplateId, level, basicAttackSkillId, skillIds);
            _players[playerId.Value] = slot;
            Revision++;
            return true;
        }

        public bool CanStart()
        {
            var count = _players.Count;
            if (count < MinPlayers) return false;
            if (MaxPlayers > 0 && count > MaxPlayers) return false;

            foreach (var kv in _players)
            {
                var s = kv.Value;
                if (!s.Ready) return false;
                if (s.HeroId <= 0) return false;
            }
            return true;
        }

        public bool TryBuildGameStartSpec(PlayerId localPlayerId, out MobaGameStartSpec spec)
        {
            if (!CanStart())
            {
                spec = default;
                return false;
            }

            var loadouts = new List<MobaPlayerLoadout>(_players.Count);
            var spawnIndex = 0;
            foreach (var kv in _players)
            {
                var s = kv.Value;
                loadouts.Add(new MobaPlayerLoadout(
                    playerId: s.PlayerId,
                    teamId: s.TeamId,
                    heroId: s.HeroId,
                    attributeTemplateId: s.AttributeTemplateId,
                    level: s.Level,
                    basicAttackSkillId: s.BasicAttackSkillId,
                    skillIds: s.SkillIds,
                    spawnIndex: spawnIndex++));
            }

            var req = new EnterMobaGameReq(
                playerId: localPlayerId,
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                opCode: 0,
                payload: null,
                players: loadouts.ToArray());

            spec = new MobaGameStartSpec(in req);
            return true;
        }

        public MobaRoomSnapshot BuildSnapshot()
        {
            var arr = _players.Count == 0 ? null : new MobaRoomPlayerSnapshot[_players.Count];
            if (arr != null)
            {
                var i = 0;
                foreach (var kv in _players)
                {
                    var s = kv.Value;
                    arr[i++] = new MobaRoomPlayerSnapshot(
                        playerId: s.PlayerId,
                        teamId: s.TeamId,
                        ready: s.Ready,
                        heroId: s.HeroId,
                        spawnPointId: s.SpawnPointId,
                        level: s.Level,
                        attributeTemplateId: s.AttributeTemplateId,
                        basicAttackSkillId: s.BasicAttackSkillId,
                        skillIds: s.SkillIds);
                }
            }

            return new MobaRoomSnapshot(
                revision: Revision,
                matchId: MatchId,
                mapId: MapId,
                randomSeed: RandomSeed,
                tickRate: TickRate,
                inputDelayFrames: InputDelayFrames,
                minPlayers: MinPlayers,
                maxPlayers: MaxPlayers,
                canStart: CanStart(),
                players: arr);
        }

        public readonly struct PlayerSlot
        {
            public readonly PlayerId PlayerId;
            public readonly int TeamId;
            public readonly bool Ready;

            public readonly int HeroId;
            public readonly int SpawnPointId;
            public readonly int AttributeTemplateId;
            public readonly int Level;
            public readonly int BasicAttackSkillId;
            public readonly int[] SkillIds;

            public PlayerSlot(PlayerId playerId, int teamId)
            {
                PlayerId = playerId;
                TeamId = teamId;
                Ready = false;

                HeroId = 0;
                SpawnPointId = 0;
                AttributeTemplateId = 0;
                Level = 1;
                BasicAttackSkillId = 0;
                SkillIds = null;
            }

            public PlayerSlot(PlayerId playerId, int teamId, bool ready, int heroId, int spawnPointId, int attributeTemplateId, int level, int basicAttackSkillId, int[] skillIds)
            {
                PlayerId = playerId;
                TeamId = teamId;
                Ready = ready;

                HeroId = heroId;
                SpawnPointId = spawnPointId;
                AttributeTemplateId = attributeTemplateId;
                Level = level;
                BasicAttackSkillId = basicAttackSkillId;
                SkillIds = skillIds;
            }

            public PlayerSlot WithReady(bool ready)
            {
                return new PlayerSlot(PlayerId, TeamId, ready, HeroId, SpawnPointId, AttributeTemplateId, Level, BasicAttackSkillId, SkillIds);
            }

            public PlayerSlot WithHero(int heroId, int attributeTemplateId, int level, int basicAttackSkillId, int[] skillIds)
            {
                return new PlayerSlot(PlayerId, TeamId, Ready, heroId, SpawnPointId, attributeTemplateId, level, basicAttackSkillId, skillIds);
            }

            public PlayerSlot WithSpawnPoint(int spawnPointId)
            {
                return new PlayerSlot(PlayerId, TeamId, Ready, HeroId, spawnPointId, AttributeTemplateId, Level, BasicAttackSkillId, SkillIds);
            }
        }
    }
}
