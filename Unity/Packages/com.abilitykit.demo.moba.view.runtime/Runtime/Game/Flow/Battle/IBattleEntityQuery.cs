using EC = AbilityKit.Ability.EC;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    public interface IBattleEntityQuery
    {
        EC.EntityWorld World { get; }
        BattleEntityLookup Lookup { get; }

        bool TryResolve(BattleNetId netId, out EC.Entity entity);

        bool TryGetTransform(BattleNetId netId, out BattleTransformComponent transform);
        bool TryGetCharacter(BattleNetId netId, out BattleCharacterComponent character);
        bool TryGetProjectile(BattleNetId netId, out BattleProjectileComponent projectile);

        bool TryGetSkills(BattleNetId netId, out SkillListComponent skills);
        bool TryGetBuffs(BattleNetId netId, out BuffListComponent buffs);
    }
}
