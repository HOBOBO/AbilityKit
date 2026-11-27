using System;
using System.Collections.Generic;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Deterministic;
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
        /// <summary>
        /// 出生属性覆盖完成后初始化资源；仅构造阶段调用，不作为运行中治疗入口。
        /// 当前值限制在上限内，并同步生命属性和资源容器。
        /// </summary>
        public void InitializeBirthResources(global::ActorEntity entity)
        {
            var group = entity.attributeGroup.Group;
            var resources = EnsureResourceContainer(entity);
            var maxHp = group.GetValue(MobaAttributeIds.MAX_HP);
            var maxMana = group.GetValue(MobaAttributeIds.MAX_MANA);
            if (maxHp <= 0 || maxMana < 0 || float.IsNaN(maxHp) || float.IsInfinity(maxHp) ||
                float.IsNaN(maxMana) || float.IsInfinity(maxMana))
                throw new System.InvalidOperationException("Invalid summon birth resource limits.");
            var hp = group.GetValue(MobaAttributeIds.HP);
            var mana = group.GetValue(MobaAttributeIds.MANA);
            if (float.IsNaN(hp) || float.IsInfinity(hp) || float.IsNaN(mana) || float.IsInfinity(mana))
                throw new System.InvalidOperationException("Invalid summon birth resources.");
            hp = System.Math.Min(maxHp, System.Math.Max(0, hp));
            mana = System.Math.Min(maxMana, System.Math.Max(0, mana));
            group.SetBase(MobaAttributeIds.HP, hp);
            group.SetBase(MobaAttributeIds.MANA, mana);
            EnsureResource(resources, ResourceType.Hp, MobaAttributeIds.MAX_HP, hp, maxHp);
            EnsureResource(resources, ResourceType.Mana, MobaAttributeIds.MAX_MANA, mana, maxMana);
            MobaResourceAttributeContextProjector.Refresh(entity);
        }

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
            group.SetBase(MobaAttributeIds.MANA, mana);
            VisitTemplateStats(template, (id, basis, growth) => group.SetBase(id, Grow(basis, growth, levelOffset)));

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

        /// <summary>
        /// 为本地样例调试原地变更等级贡献和血蓝上限，按原比例写回现有资源。
        /// 调用方负责限制本地会话并传入当前实体对应的模板；不替代出生或玩法升级命令。
        /// </summary>
        public static bool TryApplyDebugLevel(global::ActorEntity entity, MO.BattleAttributeTemplateMO template, int level)
        {
            if (entity == null || template == null || level < 1 || !entity.hasChampionLevel ||
                !entity.hasAttributeGroup || !entity.hasResourceContainer)
                return false;

            var previousLevel = entity.championLevel.Value;
            if (previousLevel == level) return true;
            var group = entity.attributeGroup.Group;
            var context = entity.attributeGroup.Ctx;
            var resources = entity.resourceContainer.Value?.Map;
            if (previousLevel < 1 || group == null || context == null || resources == null ||
                !resources.TryGetValue(ResourceType.Hp, out var hp) || hp == null || hp.MaxAttribute != MobaAttributeIds.MAX_HP ||
                !resources.TryGetValue(ResourceType.Mana, out var mana) || mana == null || mana.MaxAttribute != MobaAttributeIds.MAX_MANA ||
                !TryGetResourceLimits(group, out var oldMaxHp, out var oldMaxMana))
                return false;

            var changes = new List<(AttributeInstance Attribute, float Previous, float Next)>();
            VisitTemplateStats(template, (id, basis, growth) =>
            {
                if (growth == 0f) return;
                var attribute = group.GetOrCreate(id);
                var previous = attribute.BaseValue;
                var delta = Grow(basis, growth, level - 1) - Grow(basis, growth, previousLevel - 1);
                changes.Add((attribute, previous, previous + delta));
            });
            foreach (var change in changes)
            {
                if (!IsFinite(change.Next)) return false;
            }

            foreach (var change in changes) change.Attribute.BaseValue = change.Next;
            if (!TryGetResourceLimits(group, out var newMaxHp, out var newMaxMana))
            {
                foreach (var change in changes) change.Attribute.BaseValue = change.Previous;
                return false;
            }

            ScaleResource(group, MobaAttributeIds.HP, hp, oldMaxHp, newMaxHp);
            ScaleResource(group, MobaAttributeIds.MANA, mana, oldMaxMana, newMaxMana);
            entity.ReplaceChampionLevel(level);
            MobaResourceAttributeContextProjector.Refresh(entity);
            return true;
        }

        private static bool TryGetResourceLimits(AttributeGroup group, out Fixed64 maxHp, out Fixed64 maxMana)
        {
            var hp = group.GetValue(MobaAttributeIds.MAX_HP);
            var mana = group.GetValue(MobaAttributeIds.MAX_MANA);
            maxHp = MobaResourceFixedConvert.ToFixed(hp);
            maxMana = MobaResourceFixedConvert.ToFixed(mana);
            return IsFinite(hp) && IsFinite(mana) && maxHp > Fixed64.Zero && mana >= 0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ScaleResource(AttributeGroup group, AttributeId currentAttribute, ResourceState resource,
            Fixed64 oldMax, Fixed64 newMax)
        {
            if (oldMax <= Fixed64.Zero || resource.Current <= Fixed64.Zero) resource.Current = Fixed64.Zero;
            else if (resource.Current >= oldMax) resource.Current = newMax;
            else if (oldMax != newMax) resource.Current = resource.Current / oldMax * newMax;
            resource.LastMax = newMax;
            group.SetBase(currentAttribute, MobaResourceFixedConvert.ToSingle(resource.Current));
        }

        private static void VisitTemplateStats(MO.BattleAttributeTemplateMO template, Action<AttributeId, float, float> visit)
        {
            visit(MobaAttributeIds.MAX_HP, template.MaxHp, template.MaxHpGrowth);
            visit(MobaAttributeIds.EXTRA_HP, template.ExtraHp, template.ExtraHpGrowth);
            visit(MobaAttributeIds.PHYSICS_ATTACK, template.PhysicsAttack, template.PhysicsAttackGrowth);
            visit(MobaAttributeIds.MAGIC_ATTACK, template.MagicAttack, template.MagicAttackGrowth);
            visit(MobaAttributeIds.EXTRA_PHYSICS_ATTACK, template.ExtraPhysicsAttack, template.ExtraPhysicsAttackGrowth);
            visit(MobaAttributeIds.EXTRA_MAGIC_ATTACK, template.ExtraMagicAttack, template.ExtraMagicAttackGrowth);
            visit(MobaAttributeIds.PHYSICS_DEFENSE, template.PhysicsDefense, template.PhysicsDefenseGrowth);
            visit(MobaAttributeIds.MAGIC_DEFENSE, template.MagicDefense, template.MagicDefenseGrowth);
            visit(MobaAttributeIds.MAX_MANA, template.MaxMana, template.MaxManaGrowth);
            visit(MobaAttributeIds.CRITICAL_R, template.CriticalR, template.CriticalRGrowth);
            visit(MobaAttributeIds.ATTACK_SPEED_R, template.AttackSpeedR, template.AttackSpeedRGrowth);
            visit(MobaAttributeIds.COOLDOWN_REDUCE_R, template.CooldownReduceR, template.CooldownReduceRGrowth);
            visit(MobaAttributeIds.PHYSICS_PENETRATION_R, template.PhysicsPenetrationR, template.PhysicsPenetrationRGrowth);
            visit(MobaAttributeIds.MAGIC_PENETRATION_R, template.MagicPenetrationR, template.MagicPenetrationRGrowth);
            visit(MobaAttributeIds.MOVE_SPEED, template.MoveSpeed, template.MoveSpeedGrowth);
            visit(MobaAttributeIds.PHYSICS_BLOODSUCKING_R, template.PhysicsBloodsuckingR, template.PhysicsBloodsuckingRGrowth);
            visit(MobaAttributeIds.MAGIC_BLOODSUCKING_R, template.MagicBloodsuckingR, template.MagicBloodsuckingRGrowth);
            visit(MobaAttributeIds.ATTACK_RANGE, template.AttackRange, template.AttackRangeGrowth);
            visit(MobaAttributeIds.PER_SECOND_BLOOD_R, template.PerSecondBloodR, template.PerSecondBloodRGrowth);
            visit(MobaAttributeIds.PER_SECOND_MANA_R, template.PerSecondManaR, template.PerSecondManaRGrowth);
            visit(MobaAttributeIds.RESILIENCE_R, template.ResilienceR, template.ResilienceRGrowth);
            visit(MobaAttributeIds.MANA_REGEN_PER_SECOND, template.ManaRegenPerSecond, template.ManaRegenPerSecondGrowth);
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
            group.GetOrCreate(MobaAttributeIds.MANA_REGEN_PER_SECOND);

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
