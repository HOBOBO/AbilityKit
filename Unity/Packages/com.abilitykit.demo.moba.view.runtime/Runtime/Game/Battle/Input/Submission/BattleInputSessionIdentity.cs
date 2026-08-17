using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    internal static class BattleInputSessionIdentity
    {
        public static PlayerId ResolvePlayerId(in BattleStartPlan plan)
        {
            var playerId = plan.World.PlayerId;
            return new PlayerId(string.IsNullOrEmpty(playerId) ? "p1" : playerId);
        }

        public static PlayerId ResolvePlayerId(IBattleInputSessionIdentityPort input)
        {
            if (input == null) return new PlayerId("p1");
            var playerId = input.ResolveLocalControlPlayerId();
            return new PlayerId(string.IsNullOrEmpty(playerId) ? "p1" : playerId);
        }

        public static bool TryResolveLocalTrainingOpponent(
            IBattleInputSessionIdentityPort input,
            PlayerId primaryPlayerId,
            out PlayerId opponentPlayerId)
        {
            opponentPlayerId = default;
            if (input == null || input.HostMode != BattleHostMode.Local) return false;

            var players = input.BuildEffectivePlayerLoadouts();
            var primaryTeamId = 0;
            for (var i = 0; i < players.Length; i++)
            {
                if (!players[i].PlayerId.Equals(primaryPlayerId)) continue;
                primaryTeamId = players[i].TeamId;
                break;
            }

            if (primaryTeamId <= 0) return false;

            for (var i = 0; i < players.Length; i++)
            {
                var candidate = players[i];
                if (candidate.PlayerId.Equals(primaryPlayerId) ||
                    string.IsNullOrEmpty(candidate.PlayerId.Value) ||
                    candidate.TeamId <= 0 ||
                    candidate.TeamId == primaryTeamId)
                {
                    continue;
                }

                opponentPlayerId = candidate.PlayerId;
                return true;
            }

            return false;
        }

        public static WorldId ResolveWorldId(in BattleStartPlan plan)
        {
            var worldId = plan.World.WorldId;
            return new WorldId(string.IsNullOrEmpty(worldId) ? "room_1" : worldId);
        }
    }
}
