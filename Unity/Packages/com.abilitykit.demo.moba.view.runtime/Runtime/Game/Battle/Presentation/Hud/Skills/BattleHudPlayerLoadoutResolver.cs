using AbilityKit.Core.Logging;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudPlayerLoadoutResolver
    {
        public bool TryFind(
            EnterMobaGameRes res,
            string playerId,
            out MobaPlayerLoadout loadout)
        {
            loadout = default;
            if (string.IsNullOrEmpty(playerId)) return false;

            var loadouts = res.PlayersLoadout;
            if (loadouts == null || loadouts.Length == 0) return false;

            if (TryFindByPlayerId(loadouts, playerId, out loadout))
            {
                return true;
            }

            var responsePlayerId = res.PlayerId.Value;
            Log.Warning($"[BattleHudPlayerLoadoutResolver] local loadout not found. requested={playerId}, response={responsePlayerId}, loadoutCount={loadouts.Length}.");
            return false;
        }

        private static bool TryFindByPlayerId(MobaPlayerLoadout[] loadouts, string playerId, out MobaPlayerLoadout loadout)
        {
            loadout = default;
            if (string.IsNullOrEmpty(playerId)) return false;
            if (loadouts == null || loadouts.Length == 0) return false;

            for (int i = 0; i < loadouts.Length; i++)
            {
                var candidate = loadouts[i];
                if (candidate.PlayerId.Value == playerId)
                {
                    loadout = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
