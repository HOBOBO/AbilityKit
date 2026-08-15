using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleDebugCommandService
    {
        bool TrySwitchControl(out string message);

        bool TrySetControlPlayer(PlayerId playerId, out string message);

        bool TryResetCooldowns(out string message);

        bool TryToggleEnemyAi(out string message);

        bool TryReplaceHero(int heroId, out string message);

        bool TrySpawnAlly(out string message);

        bool TrySpawnEnemy(out string message);
    }
}
