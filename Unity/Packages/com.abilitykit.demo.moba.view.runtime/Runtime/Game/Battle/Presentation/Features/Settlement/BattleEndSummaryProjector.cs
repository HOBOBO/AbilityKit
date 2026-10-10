using System;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    internal static class BattleEndSummaryProjector
    {
        public static BattleEndSummary Build(BattleGameplayPresentationState state)
        {
            if (state == null || !state.Deathmatch.HasEndedMatch) return new BattleEndSummary();

            var match = state.Deathmatch.Match;
            var summary = new BattleEndSummary
            {
                MatchId = match.MatchId,
                GameplayId = match.GameplayId,
                HasAuthoritativeResult = true,
                WinningTeamId = match.WinTeamId,
                Team1Score = match.Team1Score,
                Team2Score = match.Team2Score,
                EndReason = match.EndReason,
                MatchDurationFrames = match.EndFrame,
                MatchDurationSeconds = state.HasClock && state.Clock.DurationMilliseconds > 0
                    ? Math.Max(0, state.Clock.DurationMilliseconds - state.Clock.RemainingMilliseconds) / 1000
                    : match.EndFrame / state.TickRate
            };
            foreach (var player in state.Players)
            {
                var local = string.Equals(player.PlayerId.Value, state.LocalPlayerId,
                    StringComparison.OrdinalIgnoreCase);
                summary.Players.Add(new BattleEndPlayerRow
                {
                    PlayerId = player.PlayerId.Value ?? string.Empty,
                    TeamId = player.TeamId,
                    HeroId = player.HeroId,
                    IsLocalPlayer = local
                });
                if (local) summary.LocalPlayerVictory = match.WinTeamId > 0 && player.TeamId == match.WinTeamId;
            }
            return summary;
        }
    }
}
