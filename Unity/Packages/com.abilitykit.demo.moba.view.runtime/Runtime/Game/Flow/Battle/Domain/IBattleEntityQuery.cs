using EC = AbilityKit.World.ECS;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    public interface IBattleEntityQuery
    {
        EC.IECWorld World { get; }
        BattleEntityLookup Lookup { get; }

        bool TryResolve(BattleNetId netId, out EC.IEntity entity);

        bool TryGetTransform(BattleNetId netId, out BattleTransformComponent transform);
        bool TryGetCharacter(BattleNetId netId, out BattleCharacterComponent character);
        bool TryGetProjectile(BattleNetId netId, out BattleProjectileComponent projectile);

        bool TryGetSkills(BattleNetId netId, out SkillListComponent skills);
        bool TryGetBuffs(BattleNetId netId, out BuffListComponent buffs);
    }
}
