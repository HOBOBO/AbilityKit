using System.Collections.Generic;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Effect;
using MO = AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public readonly struct MobaPreparedActorAttributes
    {
        public readonly AttributeGroup Group;
        public readonly AttributeContext Context;
        public readonly ResourceContainer Resources;

        public MobaPreparedActorAttributes(
            AttributeGroup group,
            AttributeContext context,
            ResourceContainer resources)
        {
            Group = group;
            Context = context;
            Resources = resources;
        }
    }

    public sealed class MobaActorAttributeInitializer
    {
        public void EnsureContainers(global::ActorEntity entity)
        {
            if (entity == null) return;
            var group = EnsureAttributeGroup(entity);
            var resources = EnsureResourceContainer(entity);

            EnsureResourceIfMissing(
                resources,
                ResourceType.Hp,
                MobaAttributeIds.MAX_HP,
                group.GetValue(MobaAttributeIds.HP),
                group.GetValue(MobaAttributeIds.MAX_HP));
            EnsureResourceIfMissing(
                resources,
                ResourceType.Mana,
                MobaAttributeIds.MAX_MANA,
                group.GetValue(MobaAttributeIds.MANA),
                group.GetValue(MobaAttributeIds.MAX_MANA));
            EnsureResourceIfMissing(resources, ResourceType.Rage, default, 0f, 100f);
        }

        public void ApplyTemplate(global::ActorEntity entity, MO.BattleAttributeTemplateMO template)
        {
            if (entity == null || template == null) return;
            Apply(entity, Prepare(template, 1));
        }

        public MobaPreparedActorAttributes Prepare(MO.BattleAttributeTemplateMO template, int level = 1)
        {
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }

            var context = new AttributeContext();
            var group = context.GetOrCreateGroup("moba");

            var levelOffset = System.Math.Max(1, level) - 1;
            var hp = Grow(template.Hp, template.HpGrowth, levelOffset);
            var maxHp = Grow(template.MaxHp, template.MaxHpGrowth, levelOffset);
            var mana = Grow(template.Mana, template.ManaGrowth, levelOffset);
            var maxMana = Grow(template.MaxMana, template.MaxManaGrowth, levelOffset);
            group.SetBase(MobaAttributeIds.HP, hp);
            group.SetBase(MobaAttributeIds.MAX_HP, maxHp);
            group.SetBase(MobaAttributeIds.EXTRA_HP, Grow(template.ExtraHp, template.ExtraHpGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PHYSICS_ATTACK, Grow(template.PhysicsAttack, template.PhysicsAttackGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MAGIC_ATTACK, Grow(template.MagicAttack, template.MagicAttackGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.EXTRA_PHYSICS_ATTACK, Grow(template.ExtraPhysicsAttack, template.ExtraPhysicsAttackGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.EXTRA_MAGIC_ATTACK, Grow(template.ExtraMagicAttack, template.ExtraMagicAttackGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PHYSICS_DEFENSE, Grow(template.PhysicsDefense, template.PhysicsDefenseGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MAGIC_DEFENSE, Grow(template.MagicDefense, template.MagicDefenseGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MANA, mana);
            group.SetBase(MobaAttributeIds.MAX_MANA, maxMana);
            group.SetBase(MobaAttributeIds.CRITICAL_R, Grow(template.CriticalR, template.CriticalRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.ATTACK_SPEED_R, Grow(template.AttackSpeedR, template.AttackSpeedRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.COOLDOWN_REDUCE_R, Grow(template.CooldownReduceR, template.CooldownReduceRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PHYSICS_PENETRATION_R, Grow(template.PhysicsPenetrationR, template.PhysicsPenetrationRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MAGIC_PENETRATION_R, Grow(template.MagicPenetrationR, template.MagicPenetrationRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MOVE_SPEED, Grow(template.MoveSpeed, template.MoveSpeedGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PHYSICS_BLOODSUCKING_R, Grow(template.PhysicsBloodsuckingR, template.PhysicsBloodsuckingRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.MAGIC_BLOODSUCKING_R, Grow(template.MagicBloodsuckingR, template.MagicBloodsuckingRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.ATTACK_RANGE, Grow(template.AttackRange, template.AttackRangeGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PER_SECOND_BLOOD_R, Grow(template.PerSecondBloodR, template.PerSecondBloodRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.PER_SECOND_MANA_R, Grow(template.PerSecondManaR, template.PerSecondManaRGrowth, levelOffset));
            group.SetBase(MobaAttributeIds.RESILIENCE_R, Grow(template.ResilienceR, template.ResilienceRGrowth, levelOffset));

            var resources = new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>()
            };
            EnsureResource(resources, ResourceType.Hp, MobaAttributeIds.MAX_HP, hp, maxHp);
            EnsureResource(resources, ResourceType.Mana, MobaAttributeIds.MAX_MANA, mana, maxMana);
            EnsureResource(resources, ResourceType.Rage, default, 0f, 100f);
            return new MobaPreparedActorAttributes(group, context, resources);
        }

        private static float Grow(float value, float growth, int levelOffset) => value + growth * levelOffset;

        public void Apply(global::ActorEntity entity, in MobaPreparedActorAttributes prepared)
        {
            if (entity == null) return;

            if (entity.hasAttributeGroup)
            {
                entity.ReplaceAttributeGroup(prepared.Group, prepared.Context);
            }
            else
            {
                entity.AddAttributeGroup(prepared.Group, prepared.Context);
            }

            if (entity.hasResourceContainer)
            {
                entity.ReplaceResourceContainer(prepared.Resources, true);
            }
            else
            {
                entity.AddResourceContainer(prepared.Resources, true);
            }
        }

        private static AttributeGroup EnsureAttributeGroup(global::ActorEntity entity)
        {
            if (entity.hasAttributeGroup)
            {
                if (entity.attributeGroup.Ctx == null)
                {
                    entity.attributeGroup.Ctx = new AttributeContext();
                }

                if (entity.attributeGroup.Group != null)
                {
                    return entity.attributeGroup.Group;
                }

                var existingCtx = entity.attributeGroup.Ctx;
                var existingGroup = existingCtx.GetOrCreateGroup("moba");
                entity.ReplaceAttributeGroup(existingGroup, existingCtx);
                return existingGroup;
            }

            var ctx = new AttributeContext();
            var group = ctx.GetOrCreateGroup("moba");
            entity.AddAttributeGroup(group, ctx);

            group.GetOrCreate(MobaAttributeIds.HP);
            group.GetOrCreate(MobaAttributeIds.MAX_HP);
            group.GetOrCreate(MobaAttributeIds.EXTRA_HP);
            group.GetOrCreate(MobaAttributeIds.PHYSICS_ATTACK);
            group.GetOrCreate(MobaAttributeIds.MAGIC_ATTACK);
            group.GetOrCreate(MobaAttributeIds.EXTRA_PHYSICS_ATTACK);
            group.GetOrCreate(MobaAttributeIds.EXTRA_MAGIC_ATTACK);
            group.GetOrCreate(MobaAttributeIds.PHYSICS_DEFENSE);
            group.GetOrCreate(MobaAttributeIds.MAGIC_DEFENSE);
            group.GetOrCreate(MobaAttributeIds.MANA);
            group.GetOrCreate(MobaAttributeIds.MAX_MANA);
            group.GetOrCreate(MobaAttributeIds.CRITICAL_R);
            group.GetOrCreate(MobaAttributeIds.ATTACK_SPEED_R);
            group.GetOrCreate(MobaAttributeIds.COOLDOWN_REDUCE_R);
            group.GetOrCreate(MobaAttributeIds.PHYSICS_PENETRATION_R);
            group.GetOrCreate(MobaAttributeIds.MAGIC_PENETRATION_R);
            group.GetOrCreate(MobaAttributeIds.MOVE_SPEED);
            group.GetOrCreate(MobaAttributeIds.PHYSICS_BLOODSUCKING_R);
            group.GetOrCreate(MobaAttributeIds.MAGIC_BLOODSUCKING_R);
            group.GetOrCreate(MobaAttributeIds.ATTACK_RANGE);
            group.GetOrCreate(MobaAttributeIds.PER_SECOND_BLOOD_R);
            group.GetOrCreate(MobaAttributeIds.PER_SECOND_MANA_R);
            group.GetOrCreate(MobaAttributeIds.RESILIENCE_R);

            return group;
        }

        private static void MarkAttributeGroupInitialized(global::ActorEntity entity, AttributeGroup group)
        {
            if (entity.hasAttributeGroup)
            {
                var ctx = entity.attributeGroup.Ctx;
                entity.ReplaceAttributeGroup(group, ctx);
            }
            else
            {
                var ctx = new AttributeContext();
                entity.AddAttributeGroup(group, ctx);
            }
        }

        private static ResourceContainer EnsureResourceContainer(global::ActorEntity entity)
        {
            if (!entity.hasResourceContainer || entity.resourceContainer.Value == null)
            {
                var resources = new ResourceContainer { Map = new Dictionary<ResourceType, ResourceState>() };
                if (entity.hasResourceContainer) entity.ReplaceResourceContainer(resources, true);
                else entity.AddResourceContainer(resources, true);
                return resources;
            }

            if (entity.resourceContainer.Value.Map == null)
            {
                entity.resourceContainer.Value.Map = new Dictionary<ResourceType, ResourceState>();
            }

            return entity.resourceContainer.Value;
        }

        private static void EnsureResource(ResourceContainer container, ResourceType type, AttributeId maxAttr, float current, float lastMax)
        {
            if (container.Map == null) container.Map = new Dictionary<ResourceType, ResourceState>();
            if (!container.Map.TryGetValue(type, out var state) || state == null)
            {
                state = new ResourceState();
                container.Map[type] = state;
            }

            state.MaxAttribute = maxAttr;
            state.Current = MobaResourceFixedConvert.ToFixed(current);
            state.LastMax = MobaResourceFixedConvert.ToFixed(lastMax);
        }

        private static void EnsureResourceIfMissing(
            ResourceContainer container,
            ResourceType type,
            AttributeId maxAttr,
            float current,
            float lastMax)
        {
            if (container.Map == null)
            {
                container.Map = new Dictionary<ResourceType, ResourceState>();
            }

            if (container.Map.TryGetValue(type, out var state) && state != null)
            {
                return;
            }

            container.Map[type] = new ResourceState
            {
                MaxAttribute = maxAttr,
                Current = MobaResourceFixedConvert.ToFixed(current),
                LastMax = MobaResourceFixedConvert.ToFixed(lastMax)
            };
        }
    }
}
