using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudPlayerLoadoutResolver
    {
        public bool TryFind(
            BattleEnterGameSnapshot res,
            string playerId,
            out BattlePlayerLoadout loadout)
        {
            loadout = default;
            if (string.IsNullOrEmpty(playerId)) return false;

            var loadouts = res.PlayersLoadout;
            if (loadouts == null || loadouts.Length == 0) return false;

            if (TryFindByPlayerId(loadouts, playerId, out loadout))
            {
                return true;
            }

            var responsePlayerId = res.PlayerId;
            Log.Warning($"[BattleHudPlayerLoadoutResolver] local loadout not found. requested={playerId}, response={responsePlayerId}, loadoutCount={loadouts.Length}.");
            return false;
        }

        private static bool TryFindByPlayerId(BattlePlayerLoadout[] loadouts, string playerId, out BattlePlayerLoadout loadout)
        {
            loadout = default;
            if (string.IsNullOrEmpty(playerId)) return false;
            if (loadouts == null || loadouts.Length == 0) return false;

            for (int i = 0; i < loadouts.Length; i++)
            {
                var candidate = loadouts[i];
                if (string.Equals(candidate.PlayerId, playerId, System.StringComparison.OrdinalIgnoreCase))
                {
                    loadout = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
