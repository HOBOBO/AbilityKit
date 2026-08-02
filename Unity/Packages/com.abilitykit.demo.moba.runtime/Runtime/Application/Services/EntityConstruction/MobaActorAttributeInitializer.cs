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
            Apply(entity, Prepare(template));
        }

        public MobaPreparedActorAttributes Prepare(MO.BattleAttributeTemplateMO template)
        {
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }

            var context = new AttributeContext();
            var group = context.GetOrCreateGroup("moba");

            group.SetBase(MobaAttributeIds.HP, template.Hp);
            group.SetBase(MobaAttributeIds.MAX_HP, template.MaxHp);
            group.SetBase(MobaAttributeIds.EXTRA_HP, template.ExtraHp);
            group.SetBase(MobaAttributeIds.PHYSICS_ATTACK, template.PhysicsAttack);
            group.SetBase(MobaAttributeIds.MAGIC_ATTACK, template.MagicAttack);
            group.SetBase(MobaAttributeIds.EXTRA_PHYSICS_ATTACK, template.ExtraPhysicsAttack);
            group.SetBase(MobaAttributeIds.EXTRA_MAGIC_ATTACK, template.ExtraMagicAttack);
            group.SetBase(MobaAttributeIds.PHYSICS_DEFENSE, template.PhysicsDefense);
            group.SetBase(MobaAttributeIds.MAGIC_DEFENSE, template.MagicDefense);
            group.SetBase(MobaAttributeIds.MANA, template.Mana);
            group.SetBase(MobaAttributeIds.MAX_MANA, template.MaxMana);
            group.SetBase(MobaAttributeIds.CRITICAL_R, template.CriticalR);
            group.SetBase(MobaAttributeIds.ATTACK_SPEED_R, template.AttackSpeedR);
            group.SetBase(MobaAttributeIds.COOLDOWN_REDUCE_R, template.CooldownReduceR);
            group.SetBase(MobaAttributeIds.PHYSICS_PENETRATION_R, template.PhysicsPenetrationR);
            group.SetBase(MobaAttributeIds.MAGIC_PENETRATION_R, template.MagicPenetrationR);
            group.SetBase(MobaAttributeIds.MOVE_SPEED, template.MoveSpeed);
            group.SetBase(MobaAttributeIds.PHYSICS_BLOODSUCKING_R, template.PhysicsBloodsuckingR);
            group.SetBase(MobaAttributeIds.MAGIC_BLOODSUCKING_R, template.MagicBloodsuckingR);
            group.SetBase(MobaAttributeIds.ATTACK_RANGE, template.AttackRange);
            group.SetBase(MobaAttributeIds.PER_SECOND_BLOOD_R, template.PerSecondBloodR);
            group.SetBase(MobaAttributeIds.PER_SECOND_MANA_R, template.PerSecondManaR);
            group.SetBase(MobaAttributeIds.RESILIENCE_R, template.ResilienceR);

            var resources = new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>()
            };
            EnsureResource(resources, ResourceType.Hp, MobaAttributeIds.MAX_HP, template.Hp, template.MaxHp);
            EnsureResource(resources, ResourceType.Mana, MobaAttributeIds.MAX_MANA, template.Mana, template.MaxMana);
            EnsureResource(resources, ResourceType.Rage, default, 0f, 100f);
            return new MobaPreparedActorAttributes(group, context, resources);
        }

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
            state.Current = current;
            state.LastMax = lastMax;
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
                Current = current,
                LastMax = lastMax
            };
        }
    }
}
