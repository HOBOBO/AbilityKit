using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleDebugCommandService
    {
        bool TrySwitchControl(out string message);

        bool TrySetControlPlayer(PlayerId playerId, out string message);

        bool TryResetCooldowns(out string message);

        bool TrySetTrainingOptions(bool noCooldown, bool infiniteMana, out string message);

        bool TryRestoreResources(out string message);

        /// <summary>
        /// 仅调整当前主控英雄指定槽位的技能 Rank，不改变英雄等级。
        /// </summary>
        bool TrySetSkillRank(int slot, int rank, out string message);

        bool TrySpawnDummy(float offsetX, float offsetZ, out string message);

        bool TryClearDummies(out string message);

        /// <summary>
        /// 将本地战斗中当前主控英雄的等级提高一级，不改变技能 Rank。
        /// </summary>
        bool TryIncreaseHeroLevel(out string message);

        /// <summary>
        /// 将本地战斗中当前主控英雄的等级重置为一级。
        /// </summary>
        bool TryResetHeroLevel(out string message);

        bool TryToggleEnemyAi(out string message);

        bool TryReplaceHero(int heroId, out string message);

        bool TrySpawnAlly(out string message);

        bool TrySpawnEnemy(out string message);
    }
}
