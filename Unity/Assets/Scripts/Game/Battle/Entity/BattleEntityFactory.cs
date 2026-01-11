using System;
using EC = AbilityKit.Ability.EC;
using AbilityKit.Game.Battle.Component;

namespace AbilityKit.Game.Battle.Entity
{
    public sealed class BattleEntityFactory
    {
        private readonly EC.EntityWorld _world;
        private readonly BattleEntityLookup _lookup;
        private readonly EC.Entity _parent;

        public BattleEntityFactory(EC.EntityWorld world, BattleEntityLookup lookup = null, EC.Entity parent = default)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lookup = lookup;
            _parent = parent;
        }

        public EC.Entity CreateCharacter(BattleNetId netId, int entityCode = 0)
        {
            var e = _parent.IsValid ? _parent.AddChild($"Actor_{netId.Value}") : _world.Create();
            e.AddComponent(new BattleNetIdComponent { NetId = netId });
            e.AddComponent(new BattleEntityMetaComponent { Kind = BattleEntityKind.Character, EntityCode = entityCode });
            e.AddComponent(new BattleTransformComponent());
            e.AddComponent(new BattleCharacterComponent());
            e.AddComponent(new SkillListComponent());
            e.AddComponent(new BuffListComponent());

            _lookup?.Bind(netId, e);
            return e;
        }

        public EC.Entity CreateProjectile(BattleNetId netId, BattleNetId ownerNetId, int entityCode = 0)
        {
            var e = _parent.IsValid ? _parent.AddChild($"Projectile_{netId.Value}") : _world.Create();
            e.AddComponent(new BattleNetIdComponent { NetId = netId });
            e.AddComponent(new BattleEntityMetaComponent { Kind = BattleEntityKind.Projectile, EntityCode = entityCode });
            e.AddComponent(new BattleTransformComponent());
            e.AddComponent(new BattleProjectileComponent { OwnerNetId = ownerNetId });

            _lookup?.Bind(netId, e);
            return e;
        }
    }
}
