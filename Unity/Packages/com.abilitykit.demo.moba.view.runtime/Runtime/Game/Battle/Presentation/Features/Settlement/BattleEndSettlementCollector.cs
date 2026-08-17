using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    internal readonly struct BattleEndPlayerProjectionInput
    {
        public BattleEndPlayerProjectionInput(
            string playerId,
            int teamId,
            int heroId,
            bool isLocalPlayer,
            int finalHp,
            int maxHp,
            bool isAlive)
        {
            PlayerId = playerId ?? string.Empty;
            TeamId = teamId;
            HeroId = heroId;
            IsLocalPlayer = isLocalPlayer;
            FinalHp = finalHp;
            MaxHp = maxHp;
            IsAlive = isAlive;
        }

        public string PlayerId { get; }
        public int TeamId { get; }
        public int HeroId { get; }
        public bool IsLocalPlayer { get; }
        public int FinalHp { get; }
        public int MaxHp { get; }
        public bool IsAlive { get; }
    }

    internal sealed class BattleEndSettlementProjection
    {
        public BattleEndSettlementProjection(
            int matchDurationFrames,
            int matchDurationSeconds,
            int winningTeamId,
            bool localPlayerVictory,
            IReadOnlyList<BattleEndPlayerProjectionInput> players)
        {
            MatchDurationFrames = matchDurationFrames;
            MatchDurationSeconds = matchDurationSeconds;
            WinningTeamId = winningTeamId;
            LocalPlayerVictory = localPlayerVictory;
            Players = players ?? Array.Empty<BattleEndPlayerProjectionInput>();
        }

        public int MatchDurationFrames { get; }
        public int MatchDurationSeconds { get; }
        public int WinningTeamId { get; }
        public bool LocalPlayerVictory { get; }
        public IReadOnlyList<BattleEndPlayerProjectionInput> Players { get; }
    }

    internal sealed class BattleEndSettlementCollector
    {
        private readonly List<BattleEndPlayerProjectionInput> _players =
            new List<BattleEndPlayerProjectionInput>(10);

        public void Reset()
        {
            _players.Clear();
        }

        public void AddPlayer(in BattleEndPlayerProjectionInput player)
        {
            _players.Add(player);
        }

        public BattleEndSettlementProjection Build(int startFrame, int lastFrame, int tickRate = 30)
        {
            var durationFrames = Math.Max(0, lastFrame - startFrame);
            var durationSeconds = tickRate > 0 ? durationFrames / tickRate : 0;
            var winningTeamId = 0;
            var localPlayerVictory = false;

            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (!player.IsLocalPlayer) continue;

                winningTeamId = player.TeamId;
                localPlayerVictory = player.IsAlive;
                break;
            }

            return new BattleEndSettlementProjection(
                durationFrames,
                durationSeconds,
                winningTeamId,
                localPlayerVictory,
                _players.ToArray());
        }
    }
}
