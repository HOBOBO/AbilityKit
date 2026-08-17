using System;

namespace AbilityKit.Game.Flow
{
    internal static class FormalLobbyDecision
    {
        public static bool ShouldEnterBattle(
            bool remoteSelected,
            MultiplayerRoomFlowState state,
            MultiplayerRoomSnapshot snapshot)
        {
            return remoteSelected && MultiplayerBattleEntryGate.CanEnter(state, snapshot);
        }

        public static MultiplayerRoomPlayerSnapshot FindLocalPlayer(
            MultiplayerRoomSnapshot snapshot,
            uint localPlayerId,
            string accountId)
        {
            var players = snapshot?.Players;
            if (players == null) return null;

            if (!string.IsNullOrWhiteSpace(accountId))
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (string.Equals(players[i].AccountId, accountId, StringComparison.Ordinal))
                    {
                        return players[i];
                    }
                }
            }

            if (localPlayerId != 0u)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    if (players[i].PlayerId == localPlayerId) return players[i];
                }
            }

            return null;
        }

        public static bool IsOwnerAbsent(MultiplayerRoomSnapshot snapshot)
        {
            if (snapshot == null) return false;
            var owner = snapshot.OwnerAccountId;
            if (string.IsNullOrWhiteSpace(owner)) return false;
            var players = snapshot.Players;
            if (players == null) return false;
            for (var i = 0; i < players.Count; i++)
            {
                if (string.Equals(players[i].AccountId, owner, StringComparison.Ordinal))
                {
                    return !players[i].IsOnline;
                }
            }

            return true;
        }

        public static MultiplayerLoadoutSpec ResolveAvailableDefaultLoadout(
            MultiplayerLoadoutSpec configured,
            MultiplayerRoomSnapshot snapshot,
            uint localPlayerId)
        {
            var teamId = configured.TeamId;
            var spawnPointId = configured.SpawnPointId;
            var players = snapshot?.Players;
            if (players != null)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.PlayerId == localPlayerId || player.HeroId <= 0) continue;
                    if (player.TeamId == teamId && player.HeroId == configured.HeroId)
                    {
                        teamId = teamId == 1 ? 2 : 1;
                    }
                    if (player.TeamId == teamId && player.SpawnPointId == spawnPointId)
                    {
                        spawnPointId++;
                    }
                }
            }

            return new MultiplayerLoadoutSpec(
                configured.HeroId,
                teamId,
                spawnPointId,
                configured.Level,
                configured.AttributeTemplateId,
                configured.BasicAttackSkillId,
                configured.SkillIds);
        }
    }
}
