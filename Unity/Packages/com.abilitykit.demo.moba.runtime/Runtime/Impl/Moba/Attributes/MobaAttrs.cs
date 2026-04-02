using System;
using AbilityKit.Ability.Impl.Moba.Components;
using AbilityKit.Ability.Share.Common.AttributeSystem;

namespace AbilityKit.Ability.Impl.Moba.Attributes
{
    public readonly struct MobaAttrs
    {
        private readonly ActorEntity _entity;

        public MobaAttrs(ActorEntity entity)
        {
            _entity = entity;
        }

        public ActorEntity Entity => _entity;

        private AttributeGroup RequireGroup()
        {
            if (_entity == null || !_entity.hasAttributeGroup || _entity.attributeGroup.Group == null)
                throw new InvalidOperationException("ActorEntity has no AttributeGroup.");
            return _entity.attributeGroup.Group;
        }

        private ResourceContainer RequireResources()
        {
            if (_entity == null || !_entity.hasResourceContainer || _entity.resourceContainer.Value == null)
                throw new InvalidOperationException("ActorEntity has no ResourceContainer.");
            return _entity.resourceContainer.Value;
        }

        private static float GetResourceCurrent(ResourceContainer container, ResourceType type)
        {
            if (container.Map == null) return 0f;
            return container.Map.TryGetValue(type, out var s) && s != null ? s.Current : 0f;
        }

        private static void SetResourceCurrent(ResourceContainer container, ResourceType type, float value)
        {
            if (container.Map == null) container.Map = new System.Collections.Generic.Dictionary<ResourceType, ResourceState>();
            if (!container.Map.TryGetValue(type, out var s) || s == null)
            {
                s = new ResourceState();
                container.Map[type] = s;
            }

            s.Current = value;
        }

        public float Hp
        {
            get => GetResourceCurrent(RequireResources(), ResourceType.Hp);
            set => SetResourceCurrent(RequireResources(), ResourceType.Hp, value);
        }

        public float Mana
        {
            get => GetResourceCurrent(RequireResources(), ResourceType.Mana);
            set => SetResourceCurrent(RequireResources(), ResourceType.Mana, value);
        }

        public float MaxHp => RequireGroup().GetValue(MobaAttributeIds.MAX_HP);
        public float ExtraHp => RequireGroup().GetValue(MobaAttributeIds.EXTRA_HP);
        public float PhysicsAttack => RequireGroup().GetValue(MobaAttributeIds.PHYSICS_ATTACK);
        public float MagicAttack => RequireGroup().GetValue(MobaAttributeIds.MAGIC_ATTACK);
        public float ExtraPhysicsAttack => RequireGroup().GetValue(MobaAttributeIds.EXTRA_PHYSICS_ATTACK);
        public float ExtraMagicAttack => RequireGroup().GetValue(MobaAttributeIds.EXTRA_MAGIC_ATTACK);
        public float PhysicsDefense => RequireGroup().GetValue(MobaAttributeIds.PHYSICS_DEFENSE);
        public float MagicDefense => RequireGroup().GetValue(MobaAttributeIds.MAGIC_DEFENSE);
        public float MaxMana => RequireGroup().GetValue(MobaAttributeIds.MAX_MANA);

        public float CriticalR => RequireGroup().GetValue(MobaAttributeIds.CRITICAL_R);
        public float AttackSpeedR => RequireGroup().GetValue(MobaAttributeIds.ATTACK_SPEED_R);
        public float CooldownReduceR => RequireGroup().GetValue(MobaAttributeIds.COOLDOWN_REDUCE_R);
        public float PhysicsPenetrationR => RequireGroup().GetValue(MobaAttributeIds.PHYSICS_PENETRATION_R);
        public float MagicPenetrationR => RequireGroup().GetValue(MobaAttributeIds.MAGIC_PENETRATION_R);
        public float MoveSpeed => RequireGroup().GetValue(MobaAttributeIds.MOVE_SPEED);
        public float PhysicsBloodsuckingR => RequireGroup().GetValue(MobaAttributeIds.PHYSICS_BLOODSUCKING_R);
        public float MagicBloodsuckingR => RequireGroup().GetValue(MobaAttributeIds.MAGIC_BLOODSUCKING_R);
        public float AttackRange => RequireGroup().GetValue(MobaAttributeIds.ATTACK_RANGE);
        public float PerSecondBloodR => RequireGroup().GetValue(MobaAttributeIds.PER_SECOND_BLOOD_R);
        public float PerSecondManaR => RequireGroup().GetValue(MobaAttributeIds.PER_SECOND_MANA_R);
        public float ResilienceR => RequireGroup().GetValue(MobaAttributeIds.RESILIENCE_R);

        public float Get(BattleAttributeType type)
        {
            return RequireGroup().GetValue(MobaAttributeIds.Get(type));
        }

        public void SetBase(BattleAttributeType type, float baseValue)
        {
            RequireGroup().SetBase(MobaAttributeIds.Get(type), baseValue);
        }

        public AttributeModifierHandle AddModifier(BattleAttributeType type, AttributeModifier modifier)
        {
            return RequireGroup().AddModifier(MobaAttributeIds.Get(type), modifier);
        }

        public bool RemoveModifier(BattleAttributeType type, AttributeModifierHandle handle)
        {
            return RequireGroup().RemoveModifier(MobaAttributeIds.Get(type), handle);
        }
    }
}
