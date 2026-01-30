using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Attributes;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using UnityEngine;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    public sealed class MobaActorEntityGenerator : IService
    {
        private static readonly HashSet<int> LoggedMissingCharacterIds = new HashSet<int>();
        private static readonly HashSet<int> LoggedMissingAttributeTemplateIds = new HashSet<int>();
        private static bool LoggedMissingConfig;

        private readonly IWorldResolver _services;
        private MobaConfigDatabase _config;

        public MobaActorEntityGenerator(IWorldResolver services)
        {
            _services = services;
            TryResolveConfig();
        }

        private bool TryResolveConfig()
        {
            if (_config != null) return true;
            if (_services == null) return false;

            // TryGet swallows the exception (container TryResolve catches). We want the root cause in logs.
            if (_services.TryResolve<MobaConfigDatabase>(out var config) && config != null)
            {
                _config = config;
                return true;
            }

            // Resolve once to surface why it failed (missing registration vs. factory throwing)
            try
            {
                _config = _services.Resolve<MobaConfigDatabase>();
                return _config != null;
            }
            catch (Exception ex)
            {
                if (!LoggedMissingConfig)
                {
                    LoggedMissingConfig = true;
                    Debug.LogError($"[MobaActorEntityGenerator] Failed to resolve MobaConfigDatabase. Ensure MobaConfigWorldModule is added and config json exists in Resources. ex={ex}");
                }
                return false;
            }
        }

        public void InitializeFromAttributeTemplate(global::ActorEntity entity, int attributeTemplateId)
        {
            if (entity == null) return;

            // Always ensure base containers exist so gameplay code can safely access components
            EnsureAttributeGroup(entity);
            EnsureResourceContainer(entity);

            if (_config == null && !TryResolveConfig())
            {
                if (!LoggedMissingConfig)
                {
                    LoggedMissingConfig = true;
                    Debug.LogError("[MobaActorEntityGenerator] MobaConfigDatabase is not available. Ensure MobaConfigWorldModule is added when creating the world.");
                }
                return;
            }
            if (attributeTemplateId <= 0) return;

            AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BattleAttributeTemplateMO template;
            try
            {
                template = _config.GetAttributeTemplate(attributeTemplateId);
            }
            catch (Exception ex)
            {
                if (LoggedMissingAttributeTemplateIds.Add(attributeTemplateId))
                {
                    Debug.LogError($"[MobaActorEntityGenerator] AttributeTemplate not found. templateId={attributeTemplateId} ex={ex.Message}");
                }
                return;
            }

            if (template == null)
            {
                if (LoggedMissingAttributeTemplateIds.Add(attributeTemplateId))
                {
                    Debug.LogError($"[MobaActorEntityGenerator] AttributeTemplate is null. templateId={attributeTemplateId}");
                }
                return;
            }

            var g = EnsureAttributeGroup(entity);

            g.SetBase(MobaAttributeIds.HP, template.Hp);
            g.SetBase(MobaAttributeIds.MAX_HP, template.MaxHp);
            g.SetBase(MobaAttributeIds.EXTRA_HP, template.ExtraHp);
            g.SetBase(MobaAttributeIds.PHYSICS_ATTACK, template.PhysicsAttack);
            g.SetBase(MobaAttributeIds.MAGIC_ATTACK, template.MagicAttack);
            g.SetBase(MobaAttributeIds.EXTRA_PHYSICS_ATTACK, template.ExtraPhysicsAttack);
            g.SetBase(MobaAttributeIds.EXTRA_MAGIC_ATTACK, template.ExtraMagicAttack);
            g.SetBase(MobaAttributeIds.PHYSICS_DEFENSE, template.PhysicsDefense);
            g.SetBase(MobaAttributeIds.MAGIC_DEFENSE, template.MagicDefense);
            g.SetBase(MobaAttributeIds.MANA, template.Mana);
            g.SetBase(MobaAttributeIds.MAX_MANA, template.MaxMana);
            g.SetBase(MobaAttributeIds.CRITICAL_R, template.CriticalR);
            g.SetBase(MobaAttributeIds.ATTACK_SPEED_R, template.AttackSpeedR);
            g.SetBase(MobaAttributeIds.COOLDOWN_REDUCE_R, template.CooldownReduceR);
            g.SetBase(MobaAttributeIds.PHYSICS_PENETRATION_R, template.PhysicsPenetrationR);
            g.SetBase(MobaAttributeIds.MAGIC_PENETRATION_R, template.MagicPenetrationR);
            g.SetBase(MobaAttributeIds.MOVE_SPEED, template.MoveSpeed);
            g.SetBase(MobaAttributeIds.PHYSICS_BLOODSUCKING_R, template.PhysicsBloodsuckingR);
            g.SetBase(MobaAttributeIds.MAGIC_BLOODSUCKING_R, template.MagicBloodsuckingR);
            g.SetBase(MobaAttributeIds.ATTACK_RANGE, template.AttackRange);
            g.SetBase(MobaAttributeIds.PER_SECOND_BLOOD_R, template.PerSecondBloodR);
            g.SetBase(MobaAttributeIds.PER_SECOND_MANA_R, template.PerSecondManaR);
            g.SetBase(MobaAttributeIds.RESILIENCE_R, template.ResilienceR);

            MarkAttributeGroupInitialized(entity, g);

            var rc = EnsureResourceContainer(entity);
            EnsureResource(rc, ResourceType.Hp, MobaAttributeIds.MAX_HP, template.Hp, template.MaxHp);
            EnsureResource(rc, ResourceType.Mana, MobaAttributeIds.MAX_MANA, template.Mana, template.MaxMana);
        }

        public void InitializeFromLoadout(global::ActorEntity entity, in MobaPlayerLoadout loadout)
        {
            if (entity == null) return;

            var templateId = loadout.AttributeTemplateId;
            if (templateId <= 0 && _config != null)
            {
                try
                {
                    var character = _config.GetCharacter(loadout.HeroId);
                    templateId = character != null ? character.AttributeTemplateId : 0;
                }
                catch
                {
                    if (LoggedMissingCharacterIds.Add(loadout.HeroId))
                    {
                        Debug.LogError($"[MobaActorEntityGenerator] Character not found. heroId={loadout.HeroId}");
                    }
                    templateId = 0;
                }
            }

            if (templateId <= 0)
            {
                if (LoggedMissingAttributeTemplateIds.Add(templateId))
                {
                    Debug.LogError($"[MobaActorEntityGenerator] AttributeTemplateId is invalid. heroId={loadout.HeroId} loadoutTemplateId={loadout.AttributeTemplateId}");
                }
            }

            InitializeFromAttributeTemplate(entity, templateId);

            InitializeSkillLoadout(entity, in loadout);
        }

        private void InitializeSkillLoadout(global::ActorEntity entity, in MobaPlayerLoadout loadout)
        {
            if (entity == null) return;

            if (_config == null && !TryResolveConfig())
            {
                return;
            }

            try
            {
                var character = _config != null ? _config.GetCharacter(loadout.HeroId) : null;
                var activeSkillIds = loadout.SkillIds;
                if (activeSkillIds == null)
                {
                    activeSkillIds = ToArray(character != null ? character.SkillIds : null);
                }

                var passiveSkillIds = ToArray(character != null ? character.PassiveSkillIds : null);

                var activeSkills = CreateActiveSkillRuntimes(activeSkillIds);
                var passiveSkills = CreatePassiveSkillRuntimes(passiveSkillIds);

                if (entity.hasSkillLoadout)
                {
                    entity.ReplaceSkillLoadout(activeSkills, passiveSkills);
                }
                else
                {
                    entity.AddSkillLoadout(activeSkills, passiveSkills);
                }
            }
            catch
            {
            }
        }

        private static ActiveSkillRuntime[] CreateActiveSkillRuntimes(int[] skillIds)
        {
            if (skillIds == null || skillIds.Length == 0) return Array.Empty<ActiveSkillRuntime>();
            var list = new List<ActiveSkillRuntime>(skillIds.Length);
            for (int i = 0; i < skillIds.Length; i++)
            {
                var id = skillIds[i];
                if (id <= 0) continue;
                list.Add(new ActiveSkillRuntime { SkillId = id, Level = 1, CooldownEndTimeMs = 0L });
            }

            return list.Count == 0 ? Array.Empty<ActiveSkillRuntime>() : list.ToArray();
        }

        private static PassiveSkillRuntime[] CreatePassiveSkillRuntimes(int[] passiveSkillIds)
        {
            if (passiveSkillIds == null || passiveSkillIds.Length == 0) return Array.Empty<PassiveSkillRuntime>();
            var list = new List<PassiveSkillRuntime>(passiveSkillIds.Length);
            for (int i = 0; i < passiveSkillIds.Length; i++)
            {
                var id = passiveSkillIds[i];
                if (id <= 0) continue;
                list.Add(new PassiveSkillRuntime { PassiveSkillId = id, Level = 1, CooldownEndTimeMs = 0L });
            }

            return list.Count == 0 ? Array.Empty<PassiveSkillRuntime>() : list.ToArray();
        }

        private static int[] ToArray(IReadOnlyList<int> list)
        {
            if (list == null || list.Count == 0) return Array.Empty<int>();
            if (list is int[] arr) return arr;
            var result = new int[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i];
            return result;
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
            var g = ctx.GetOrCreateGroup("moba");
            entity.AddAttributeGroup(g, ctx);

            g.GetOrCreate(MobaAttributeIds.HP);
            g.GetOrCreate(MobaAttributeIds.MAX_HP);
            g.GetOrCreate(MobaAttributeIds.EXTRA_HP);
            g.GetOrCreate(MobaAttributeIds.PHYSICS_ATTACK);
            g.GetOrCreate(MobaAttributeIds.MAGIC_ATTACK);
            g.GetOrCreate(MobaAttributeIds.EXTRA_PHYSICS_ATTACK);
            g.GetOrCreate(MobaAttributeIds.EXTRA_MAGIC_ATTACK);
            g.GetOrCreate(MobaAttributeIds.PHYSICS_DEFENSE);
            g.GetOrCreate(MobaAttributeIds.MAGIC_DEFENSE);
            g.GetOrCreate(MobaAttributeIds.MANA);
            g.GetOrCreate(MobaAttributeIds.MAX_MANA);
            g.GetOrCreate(MobaAttributeIds.CRITICAL_R);
            g.GetOrCreate(MobaAttributeIds.ATTACK_SPEED_R);
            g.GetOrCreate(MobaAttributeIds.COOLDOWN_REDUCE_R);
            g.GetOrCreate(MobaAttributeIds.PHYSICS_PENETRATION_R);
            g.GetOrCreate(MobaAttributeIds.MAGIC_PENETRATION_R);
            g.GetOrCreate(MobaAttributeIds.MOVE_SPEED);
            g.GetOrCreate(MobaAttributeIds.PHYSICS_BLOODSUCKING_R);
            g.GetOrCreate(MobaAttributeIds.MAGIC_BLOODSUCKING_R);
            g.GetOrCreate(MobaAttributeIds.ATTACK_RANGE);
            g.GetOrCreate(MobaAttributeIds.PER_SECOND_BLOOD_R);
            g.GetOrCreate(MobaAttributeIds.PER_SECOND_MANA_R);
            g.GetOrCreate(MobaAttributeIds.RESILIENCE_R);

            return g;
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
                var rc = new ResourceContainer { Map = new Dictionary<ResourceType, ResourceState>() };
                if (entity.hasResourceContainer) entity.ReplaceResourceContainer(rc, true);
                else entity.AddResourceContainer(rc, true);
                return rc;
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
            if (!container.Map.TryGetValue(type, out var s) || s == null)
            {
                s = new ResourceState();
                container.Map[type] = s;
            }

            s.MaxAttribute = maxAttr;
            s.Current = current;
            s.LastMax = lastMax;
        }

        public void Dispose()
        {
        }
    }
}
