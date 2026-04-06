using System;
using EC = AbilityKit.World.ECS;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleEntityQuery : IBattleEntityQuery
    {
        public BattleEntityQuery(EC.IECWorld world, BattleEntityLookup lookup)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        public EC.IECWorld World { get; }
        public BattleEntityLookup Lookup { get; }

        public bool TryResolve(BattleNetId netId, out EC.IEntity entity)
        {
            return Lookup.TryResolve(World, netId, out entity);
        }

        public bool TryGetTransform(BattleNetId netId, out BattleTransformComponent transform)
        {
            transform = null;
            if (!TryResolve(netId, out var e)) return false;
            return e.TryGetRef(out transform) && transform != null;
        }

        public bool TryGetCharacter(BattleNetId netId, out BattleCharacterComponent character)
        {
            character = null;
            if (!TryResolve(netId, out var e)) return false;
            return e.TryGetRef(out character) && character != null;
        }

        public bool TryGetProjectile(BattleNetId netId, out BattleProjectileComponent projectile)
        {
            projectile = null;
            if (!TryResolve(netId, out var e)) return false;
            return e.TryGetRef(out projectile) && projectile != null;
        }

        public bool TryGetSkills(BattleNetId netId, out SkillListComponent skills)
        {
            skills = null;
            if (!TryResolve(netId, out var e)) return false;
            return e.TryGetRef(out skills) && skills != null;
        }

        public bool TryGetBuffs(BattleNetId netId, out BuffListComponent buffs)
        {
            buffs = null;
            if (!TryResolve(netId, out var e)) return false;
            return e.TryGetRef(out buffs) && buffs != null;
        }
    }
}
