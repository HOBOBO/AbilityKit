using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleConfigReferenceValidator : IMobaRuntimeValidator
    {
        private const string Source = "battle.config.references";
        private const int DefaultSkillReleaseTriggerId = 900101011;
        private const int DefaultSkillCommitTriggerId = 900101012;

        public string Name => Source;

        public void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            if (!context.TryResolve<MobaConfigDatabase>(out var config) || config == null)
            {
                report.Error(Source, "config.database", "MobaConfigDatabase is not resolved; battle config references cannot be validated.");
                return;
            }

            context.TryResolve<TriggerPlanJsonDatabase>(out var triggers);

            ValidateBattleAttributeTemplates(config, report);
            ValidateSkills(config, triggers, report);
            ValidateSkillButtonTemplates(config, report);
            ValidatePassiveSkills(config, triggers, report);
            ValidateBuffs(config, triggers, report);
            ValidateContinuousProcesses(config, triggers, report);
            ValidateCharacters(config, report);
            ValidateProjectiles(config, triggers, report);
            ValidateProjectileLaunchers(config, report);
            ValidateSummons(config, report);
            ValidateAreas(config, triggers, report);
            ValidateSpawnAreaEffectiveTimings(
                triggers,
                id => config.TryGetAoe(id, out var area) ? area : null,
                report);
            ValidateGameplay(config, triggers, report);
            ValidateTagTemplates(config, report);
            ValidateContinuousTagTemplates(config, report);
        }

        private static void ValidateBattleAttributeTemplates(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var template in All<BattleAttributeTemplateMO>(config))
            {
                if (template == null) continue;
                var path = $"battleAttributeTemplate.{template.Id}";

                if (template.Hp < 0)
                {
                    report.Warning(Source, path + ".hp", "battle attribute template hp is negative.", template.Id.ToString());
                }

                if (template.MaxHp < 0)
                {
                    report.Warning(Source, path + ".maxHp", "battle attribute template max hp is negative.", template.Id.ToString());
                }

                if (template.MaxHp > 0 && template.Hp > template.MaxHp)
                {
                    report.Warning(Source, path + ".hp", "battle attribute template hp exceeds max hp.", template.Id.ToString());
                }

                RequiredRef(Ref<SkillMO>(config.TryGetSkill), template.BasicAttackSkillId, report, path + ".basicAttackSkillId", "basic attack skill", template.Id);
                if (template.BasicAttackSkillId > 0 &&
                    config.TryGetSkill(template.BasicAttackSkillId, out var basicAttack) &&
                    basicAttack != null &&
                    basicAttack.SkillType != SkillType.NormalAttack)
                {
                    report.Error(Source, path + ".basicAttackSkillId", "configured basic attack skill is not a normal attack.", template.BasicAttackSkillId.ToString());
                }

                ValidateRefs(Ref<SkillMO>(config.TryGetSkill), template.ActiveSkills, report, path + ".activeSkills", "skill", template.Id);
                ValidateRefs(Ref<PassiveSkillMO>(config.TryGetPassiveSkill), template.PassiveSkills, report, path + ".passiveSkills", "passive skill", template.Id);

                if (template.MaxMana <= 0 && HasPositiveSkillCost(config, template.ActiveSkills))
                {
                    report.Error(
                        Source,
                        path + ".maxMana",
                        "battle attribute template has positive-cost active skills but no mana capacity.",
                        template.Id.ToString(),
                        code: "moba.skill.contract.attribute_template_missing_mana",
                        category: MobaRuntimeValidationCategory.Config,
                        businessNumericId: template.Id);
                }
            }
        }

        private static bool HasPositiveSkillCost(MobaConfigDatabase config, IReadOnlyList<int> skillIds)
        {
            if (config == null || skillIds == null || skillIds.Count == 0) return false;

            for (var i = 0; i < skillIds.Count; i++)
            {
                if (!config.TryGetSkill(skillIds[i], out var skill) || skill == null || skill.LevelTableId <= 0) continue;
                if (!config.TryGetSkillLevelTable(skill.LevelTableId, out var table) || table?.Levels == null) continue;

                for (var levelIndex = 0; levelIndex < table.Levels.Count; levelIndex++)
                {
                    if (table.Levels[levelIndex]?.Cost > 0) return true;
                }
            }

            return false;
        }

        private static void ValidateCharacters(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var character in All<CharacterMO>(config))
            {
                if (character == null) continue;
                var path = $"character.{character.Id}";

                OptionalRef(Ref<ModelMO>(config.TryGetModel), character.ModelId, report, path + ".modelId", "model", character.Id);
                RequiredRef(Ref<BattleAttributeTemplateMO>(config.TryGetAttributeTemplate), character.AttributeTemplateId, report, path + ".attributeTemplateId", "attribute template", character.Id);
                ValidateRefs(Ref<SkillMO>(config.TryGetSkill), character.SkillIds, report, path + ".skillIds", "skill", character.Id);
                ValidateRefs(Ref<PassiveSkillMO>(config.TryGetPassiveSkill), character.PassiveSkillIds, report, path + ".passiveSkillIds", "passive skill", character.Id);
            }
        }

        private static void ValidateSkills(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var skill in All<SkillMO>(config))
            {
                if (skill == null) continue;
                var path = $"skill.{skill.Id}";

                OptionalRef(Ref<SkillButtonTemplateMO>(config.TryGetSkillButtonTemplate), skill.SkillButtonTemplateId, report, path + ".skillButtonTemplateId", "skill button template", skill.Id);
                OptionalRef(Ref<SkillLevelTableMO>(config.TryGetSkillLevelTable), skill.LevelTableId, report, path + ".levelTableId", "skill level table", skill.Id);
                OptionalRef(Ref<SkillFlowMO>(config.TryGetSkillFlow), skill.PreCastFlowId, report, path + ".preCastFlowId", "pre-cast skill flow", skill.Id);
                RequiredRef(Ref<SkillFlowMO>(config.TryGetSkillFlow), skill.CastFlowId, report, path + ".castFlowId", "cast skill flow", skill.Id);

                if (skill.CooldownMs < 0)
                {
                    report.Error(
                        Source,
                        path + ".cooldownMs",
                        "skill cooldown is negative.",
                        skill.Id.ToString(),
                        code: "moba.skill.configuration.negative_cooldown",
                        category: MobaRuntimeValidationCategory.Config,
                        businessNumericId: skill.Id);
                }

                var hasPositiveCost = ValidateSkillLevelConfiguration(config, skill, report, path);
                ValidateSkillResourceContract(config, skill, hasPositiveCost, report, path);

                if (skill.Range < 0)
                {
                    report.Warning(Source, path + ".range", "skill range is negative.", skill.Id.ToString());
                }

                if (skill.CastFlowId > 0 && config.TryGetSkillFlow(skill.CastFlowId, out var castFlow))
                {
                    ValidateSkillFlow(config, triggers, report, castFlow, $"skill.{skill.Id}.castFlow.{skill.CastFlowId}", skill.Id);
                }

                if (skill.PreCastFlowId > 0 && config.TryGetSkillFlow(skill.PreCastFlowId, out var preCastFlow))
                {
                    ValidateSkillFlow(config, triggers, report, preCastFlow, $"skill.{skill.Id}.preCastFlow.{skill.PreCastFlowId}", skill.Id);
                }
            }
        }

        private static bool ValidateSkillLevelConfiguration(
            MobaConfigDatabase config,
            SkillMO skill,
            MobaRuntimeValidationReport report,
            string path)
        {
            if (skill.LevelTableId <= 0) return false;
            if (!config.TryGetSkillLevelTable(skill.LevelTableId, out var table) || table == null) return false;

            var levels = table.Levels;
            if (levels == null || levels.Count == 0)
            {
                report.Error(
                    Source,
                    path + ".levelTable.levels",
                    "skill level table has no levels.",
                    skill.Id.ToString(),
                    code: "moba.skill.configuration.empty_level_table",
                    category: MobaRuntimeValidationCategory.Config,
                    businessNumericId: skill.Id);
                return false;
            }

            var hasPositiveCost = false;
            for (var i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var levelPath = $"{path}.levelTable.levels[{i}]";
                if (level == null)
                {
                    report.Error(
                        Source,
                        levelPath,
                        "skill level entry is null.",
                        skill.Id.ToString(),
                        code: "moba.skill.configuration.null_level",
                        category: MobaRuntimeValidationCategory.Config,
                        businessNumericId: skill.Id);
                    continue;
                }

                if (level.Cost < 0)
                {
                    report.Error(
                        Source,
                        levelPath + ".cost",
                        "skill level cost is negative.",
                        skill.Id.ToString(),
                        code: "moba.skill.configuration.negative_cost",
                        category: MobaRuntimeValidationCategory.Config,
                        businessNumericId: skill.Id);
                }
                else if (level.Cost > 0)
                {
                    hasPositiveCost = true;
                }

                if (level.CooldownMs < 0)
                {
                    report.Error(
                        Source,
                        levelPath + ".cooldownMs",
                        "skill level cooldown is negative.",
                        skill.Id.ToString(),
                        code: "moba.skill.configuration.negative_level_cooldown",
                        category: MobaRuntimeValidationCategory.Config,
                        businessNumericId: skill.Id);
                }
            }

            return hasPositiveCost;
        }

        private static void ValidateSkillResourceContract(
            MobaConfigDatabase config,
            SkillMO skill,
            bool hasPositiveCost,
            MobaRuntimeValidationReport report,
            string path)
        {
            if (!hasPositiveCost) return;

            if (skill.SkillType == SkillType.NormalAttack)
            {
                report.Error(
                    Source,
                    path + ".levelTable.levels",
                    "normal attack skill must have zero resource cost.",
                    skill.Id.ToString(),
                    code: "moba.skill.contract.normal_attack_has_cost",
                    category: MobaRuntimeValidationCategory.Config,
                    businessNumericId: skill.Id);
                return;
            }

            if (skill.CastFlowId <= 0 ||
                !config.TryGetSkillFlow(skill.CastFlowId, out var flow) ||
                flow == null)
            {
                return;
            }

            var hasRelease = ContainsSkillRulePlanTrigger(flow.Phases, DefaultSkillReleaseTriggerId);
            var hasCommit = ContainsSkillRulePlanTrigger(flow.Phases, DefaultSkillCommitTriggerId);
            if (hasRelease && hasCommit) return;

            report.Error(
                Source,
                path + $".castFlow.{skill.CastFlowId}.phases",
                $"nonzero-cost active skill requires paired default release and commit RulePlan phases. release={hasRelease}, commit={hasCommit}.",
                skill.Id.ToString(),
                code: "moba.skill.contract.release_commit_required",
                category: MobaRuntimeValidationCategory.Config,
                businessNumericId: skill.Id);
        }

        private static bool ContainsSkillRulePlanTrigger(IReadOnlyList<SkillPhaseDTO> phases, int triggerId)
        {
            if (phases == null || phases.Count == 0) return false;
            for (var i = 0; i < phases.Count; i++)
            {
                if (ContainsSkillRulePlanTrigger(phases[i], triggerId)) return true;
            }

            return false;
        }

        private static bool ContainsSkillRulePlanTrigger(SkillPhaseDTO phase, int triggerId)
        {
            if (phase == null) return false;
            if ((SkillPhaseType)phase.Type == SkillPhaseType.RulePlan &&
                phase.RulePlan?.TriggerIds != null)
            {
                for (var i = 0; i < phase.RulePlan.TriggerIds.Length; i++)
                {
                    if (phase.RulePlan.TriggerIds[i] == triggerId) return true;
                }
            }

            if (ContainsSkillRulePlanTrigger(phase.Children, triggerId)) return true;
            return phase.Repeat?.Phase != null &&
                   ContainsSkillRulePlanTrigger(phase.Repeat.Phase, triggerId);
        }

        private static void ValidateSkillButtonTemplates(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var template in All<SkillButtonTemplateMO>(config))
            {
                if (template == null) continue;
                var path = $"skillButtonTemplate.{template.Id}";

                WarnIfOutsideRange(report, path + ".aimMode", template.AimMode, 0, 1, "skill aim mode", template.Id);
                WarnIfOutsideRange(report, path + ".indicatorShape", template.IndicatorShape, 0, 8, "skill aim indicator shape", template.Id);
                WarnIfOutsideRange(report, path + ".usePointMode", template.UsePointMode, 0, 2, "skill use point mode", template.Id);

                WarnIfNegative(report, path + ".longPressSeconds", template.LongPressSeconds, "skill button long press duration", template.Id);
                WarnIfNegative(report, path + ".dragThreshold", template.DragThreshold, "skill button drag threshold", template.Id);
                WarnIfNegative(report, path + ".aimMaxRadius", template.AimMaxRadius, "skill aim max radius", template.Id);
                WarnIfNegative(report, path + ".indicatorWorldWidth", template.IndicatorWorldWidth, "skill aim indicator world width", template.Id);
                WarnIfNegative(report, path + ".selectRange", template.SelectRange, "skill target select range", template.Id);
                WarnIfNegative(report, path + ".sectorAngleDegrees", template.SectorAngleDegrees, "skill sector angle", template.Id);
                WarnIfNegative(report, path + ".dashDistance", template.DashDistance, "skill dash distance", template.Id);
                WarnIfNegative(report, path + ".lockOnDurationMs", template.LockOnDurationMs, "skill lock-on duration", template.Id);
                WarnIfNegative(report, path + ".fanRadius", template.FanRadius, "skill fan radius", template.Id);
                WarnIfNegative(report, path + ".fanAngleDegrees", template.FanAngleDegrees, "skill fan angle", template.Id);
                WarnIfNegative(report, path + ".selfRadius", template.SelfRadius, "skill self radius", template.Id);
                WarnIfNegative(report, path + ".lockProjectileRadius", template.LockProjectileRadius, "skill lock projectile radius", template.Id);
            }
        }

        private static void WarnIfNegative(MobaRuntimeValidationReport report, string path, float value, string label, int businessId)
        {
            if (value < 0f)
            {
                report.Warning(Source, path, label + " is negative.", businessId.ToString());
            }
        }

        private static void WarnIfNegative(MobaRuntimeValidationReport report, string path, int value, string label, int businessId)
        {
            if (value < 0)
            {
                report.Warning(Source, path, label + " is negative.", businessId.ToString());
            }
        }

        private static void WarnIfOutsideRange(MobaRuntimeValidationReport report, string path, int value, int minimum, int maximum, string label, int businessId)
        {
            if (value < minimum || value > maximum)
            {
                report.Warning(Source, path, label + $" value {value} is outside supported range [{minimum}, {maximum}].", businessId.ToString());
            }
        }

        private static void ValidatePassiveSkills(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var passive in All<PassiveSkillMO>(config))
            {
                if (passive == null) continue;
                ValidateTriggerRefs(triggers, passive.TriggerIds, report, $"passiveSkill.{passive.Id}.triggerIds", passive.Id, TriggerPlanScope.OwnerBound);
                ValidateRefs(Ref<ContinuousProcessMO>(config.TryGetContinuousProcess), passive.ContinuousProcessIds, report, $"passiveSkill.{passive.Id}.continuousProcessIds", "continuous process", passive.Id);
            }
        }

        private static void ValidateBuffs(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var buff in All<BuffMO>(config))
            {
                if (buff == null) continue;
                var path = $"buff.{buff.Id}";

                if (buff.DurationMs < 0)
                {
                    report.Warning(Source, path + ".durationMs", "buff duration is negative; use zero only for explicit instant/permanent semantics.", buff.Id.ToString());
                }

                if (buff.MaxStacks < 0)
                {
                    report.Warning(Source, path + ".maxStacks", "buff max stacks is negative.", buff.Id.ToString());
                }

                if (buff.IntervalMs < 0)
                {
                    report.Warning(Source, path + ".intervalMs", "buff interval is negative.", buff.Id.ToString());
                }

                if (buff.IntervalMs <= 0 && buff.OnIntervalEffects != null && buff.OnIntervalEffects.Count > 0)
                {
                    report.Warning(Source, path + ".intervalMs", "buff has interval effects but no positive interval configured.", buff.Id.ToString());
                }

                if (buff.IntervalMs > 0 && (buff.OnIntervalEffects == null || buff.OnIntervalEffects.Count == 0))
                {
                    report.Warning(Source, path + ".onIntervalEffects", "buff interval is configured but no interval effects are defined.", buff.Id.ToString());
                }

                if (buff.StackingPolicy == BuffStackingPolicy.IgnoreIfExists && buff.MaxStacks > 1)
                {
                    report.Warning(Source, path + ".stackingPolicy", "ignore-if-exists buff should not rely on multi-stack semantics.", buff.Id.ToString());
                }

                ValidateTriggerRefs(triggers, buff.OnAddEffects, report, path + ".onAddEffects", buff.Id);
                ValidateTriggerRefs(triggers, buff.OnRemoveEffects, report, path + ".onRemoveEffects", buff.Id);
                ValidateTriggerRefs(triggers, buff.OnIntervalEffects, report, path + ".onIntervalEffects", buff.Id);
                ValidateTriggerRefs(triggers, buff.TriggerIds, report, path + ".triggerIds", buff.Id, TriggerPlanScope.OwnerBound);
                OptionalRef(Ref<ContinuousTagTemplateMO>(config.TryGetContinuousTagTemplate), buff.ContinuousTagTemplateId, report, path + ".continuousTagTemplateId", "continuous tag template", buff.Id);
                OptionalRef((int id, out PresentationTemplateMO value) => TryGetTableRef(config, id, out value), buff.PresentationTemplateId, report, path + ".presentationTemplateId", "presentation template", buff.Id);

                ValidateBuffModifiers(config, buff, report);
            }
        }

        private static void ValidateContinuousProcesses(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var process in All<ContinuousProcessMO>(config))
            {
                if (process == null) continue;
                var path = $"continuousProcess.{process.Id}";

                if (process.DurationMs < 0)
                {
                    report.Warning(Source, path + ".durationMs", "continuous process duration is negative; use zero only for explicit indefinite semantics.", process.Id.ToString());
                }

                if (process.IntervalMs < 0)
                {
                    report.Warning(Source, path + ".intervalMs", "continuous process interval is negative.", process.Id.ToString());
                }

                if (process.IntervalMs <= 0 && process.IntervalTriggerIds != null && process.IntervalTriggerIds.Count > 0)
                {
                    report.Warning(Source, path + ".intervalMs", "continuous process has interval triggers but no positive interval configured.", process.Id.ToString());
                }

                if (process.IntervalMs > 0 && (process.IntervalTriggerIds == null || process.IntervalTriggerIds.Count == 0))
                {
                    report.Warning(Source, path + ".intervalTriggerIds", "continuous process interval is configured but no interval triggers are defined.", process.Id.ToString());
                }

                if (process.OutOfCombatSeconds < 0)
                {
                    report.Warning(Source, path + ".outOfCombatSeconds", "continuous process out-of-combat threshold is negative.", process.Id.ToString());
                }

                ValidateTriggerRefs(triggers, process.IntervalTriggerIds, report, path + ".intervalTriggerIds", process.Id);
                ValidateTriggerRefs(triggers, process.TriggerIds, report, path + ".triggerIds", process.Id, TriggerPlanScope.OwnerBound);
                OptionalRef(Ref<ContinuousTagTemplateMO>(config.TryGetContinuousTagTemplate), process.ContinuousTagTemplateId, report, path + ".continuousTagTemplateId", "continuous tag template", process.Id);
                ValidateContinuousModifiers(config, process.Modifiers, report, path + ".modifiers", process.Id);
            }
        }

        private static void ValidateBuffModifiers(MobaConfigDatabase config, BuffMO buff, MobaRuntimeValidationReport report)
        {
            ValidateContinuousModifiers(config, buff.Modifiers, report, $"buff.{buff.Id}.modifiers", buff.Id);
        }

        private static void ValidateContinuousModifiers(MobaConfigDatabase config, IReadOnlyList<ContinuousModifierMO> modifiers, MobaRuntimeValidationReport report, string path, int businessId)
        {
            if (modifiers == null || modifiers.Count == 0) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null) continue;

                var itemPath = $"{path}[{i}]";
                if (modifier.TargetKind <= 0)
                {
                    report.Error(Source, itemPath + ".targetKind", "modifier target kind is empty.", businessId.ToString());
                }

                if (modifier.TargetId <= 0)
                {
                    report.Error(Source, itemPath + ".targetId", "modifier target id is empty.", businessId.ToString());
                }

                if (modifier.TargetKind == MobaContinuousModifierTargetKind.Attribute)
                {
                    if (!Enum.IsDefined(typeof(BattleAttributeType), modifier.TargetId) ||
                        modifier.TargetId == (int)BattleAttributeType.None)
                    {
                        report.Error(Source, itemPath + ".targetId", "battle attribute type id is invalid.", businessId.ToString());
                    }
                }
                else if (modifier.TargetKind == MobaContinuousModifierTargetKind.SkillParameter)
                {
                    ValidateSkillParameterModifierTarget(modifier.TargetId, report, itemPath + ".targetId", businessId);
                }
            }
        }

        private static void ValidateSkillParameterModifierTarget(int targetId, MobaRuntimeValidationReport report, string path, int businessId)
        {
            if (targetId >= 1 && targetId <= 5) return;

            if (targetId == 2) return;
            report.Warning(Source, path, "skill parameter modifier target is not one of the built-in projectile/summon parameter ids.", businessId.ToString());
        }

        private static void ValidateProjectiles(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var projectile in All<ProjectileMO>(config))
            {
                if (projectile == null) continue;
                var path = $"projectile.{projectile.Id}";

                if (projectile.OnHitEffectId > 0) RequiredTriggerRef(triggers, projectile.OnHitEffectId, report, path + ".onHitEffectId", projectile.Id);
                ValidateTriggerRefs(triggers, projectile.OnSpawnTriggerIds, report, path + ".onSpawnTriggerIds", projectile.Id);
                ValidateTriggerRefs(triggers, projectile.OnHitTriggerIds, report, path + ".onHitTriggerIds", projectile.Id);
                ValidateTriggerRefs(triggers, projectile.OnTickTriggerIds, report, path + ".onTickTriggerIds", projectile.Id);
                ValidateTriggerRefs(triggers, projectile.OnExitTriggerIds, report, path + ".onExitTriggerIds", projectile.Id);

                if (projectile.Speed < 0f)
                {
                    report.Warning(Source, path + ".speed", "projectile speed is negative.", projectile.Id.ToString());
                }

                if (projectile.LifetimeMs < 0)
                {
                    report.Warning(Source, path + ".lifetimeMs", "projectile lifetime is negative.", projectile.Id.ToString());
                }

                if (projectile.MaxDistance < 0f)
                {
                    report.Warning(Source, path + ".maxDistance", "projectile max distance is negative.", projectile.Id.ToString());
                }
            }
        }

        private static void ValidateProjectileLaunchers(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var launcher in All<ProjectileLauncherMO>(config))
            {
                if (launcher == null) continue;
                var path = $"projectileLauncher.{launcher.Id}";

                if (launcher.EmitterType == ProjectileEmitterType.None)
                {
                    report.Error(Source, path + ".emitterType", "projectile launcher emitter type is None.", launcher.Id.ToString());
                }

                if (launcher.DurationMs < 0)
                {
                    report.Warning(Source, path + ".durationMs", "projectile launcher duration is negative.", launcher.Id.ToString());
                }

                if (launcher.IntervalMs < 0)
                {
                    report.Warning(Source, path + ".intervalMs", "projectile launcher interval is negative.", launcher.Id.ToString());
                }

                if (launcher.DurationMs > 0 && launcher.IntervalMs <= 0)
                {
                    report.Error(Source, path + ".intervalMs", "projectile launcher duration requires a positive interval.", launcher.Id.ToString());
                }

                if (launcher.CountPerShot <= 0)
                {
                    report.Error(Source, path + ".countPerShot", "projectile launcher count per shot must be greater than zero.", launcher.Id.ToString());
                }
            }
        }

        private static void ValidateSummons(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var summon in All<SummonMO>(config))
            {
                if (summon == null) continue;
                var path = $"summon.{summon.Id}";

                OptionalRef(Ref<ModelMO>(config.TryGetModel), summon.ModelId, report, path + ".modelId", "model", summon.Id);
                RequiredRef(Ref<BattleAttributeTemplateMO>(config.TryGetAttributeTemplate), summon.AttributeTemplateId, report, path + ".attributeTemplateId", "attribute template", summon.Id);
                ValidateRefs(Ref<SkillMO>(config.TryGetSkill), summon.SkillIds, report, path + ".skillIds", "skill", summon.Id);
                ValidateRefs(Ref<PassiveSkillMO>(config.TryGetPassiveSkill), summon.PassiveSkillIds, report, path + ".passiveSkillIds", "passive skill", summon.Id);
                ValidateRefs(Ref<ComponentTemplateMO>(config.TryGetComponentTemplate), summon.DefaultComponentTemplateIds, report, path + ".defaultComponentTemplateIds", "component template", summon.Id);

                if (summon.LifetimeMs < 0)
                {
                    report.Warning(Source, path + ".lifetimeMs", "summon lifetime is negative.", summon.Id.ToString());
                }

                if (summon.MaxAlivePerOwner < 0)
                {
                    report.Warning(Source, path + ".maxAlivePerOwner", "summon max alive per owner is negative.", summon.Id.ToString());
                }

                var scales = summon.AttrScales;
                if (scales == null) continue;
                for (int i = 0; i < scales.Count; i++)
                {
                    var scale = scales[i];
                    if (scale == null) continue;
                    RequiredRef(Ref<AttrTypeMO>(config.TryGetAttrType), scale.AttrId, report, $"{path}.attrScales[{i}].attrId", "attribute type", summon.Id);
                }
            }
        }

        private static void ValidateAreas(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var area in All<AoeMO>(config))
            {
                if (area == null) continue;
                var path = $"aoe.{area.Id}";

                OptionalRef(Ref<ModelMO>(config.TryGetModel), area.ModelId, report, path + ".modelId", "model", area.Id);
                if (area.VfxId < 0)
                {
                    report.Warning(Source, path + ".vfxId", "area vfx id is negative.", area.Id.ToString());
                }

                if (area.AttachMode < 0)
                {
                    report.Warning(Source, path + ".attachMode", "area attach mode is negative.", area.Id.ToString());
                }

                ValidateTriggerRefs(triggers, area.OnDelayTriggerIds, report, path + ".onDelayTriggerIds", area.Id);
                ValidateTriggerRefs(triggers, area.OnEnterTriggerIds, report, path + ".onEnterTriggerIds", area.Id);
                ValidateTriggerRefs(triggers, area.OnExitTriggerIds, report, path + ".onExitTriggerIds", area.Id);
                ValidateTriggerRefs(triggers, area.OnIntervalTriggerIds, report, path + ".onIntervalTriggerIds", area.Id);

                if (area.Radius < 0f)
                {
                    report.Warning(Source, path + ".radius", "area radius is negative.", area.Id.ToString());
                }

                if (area.DelayMs < 0)
                {
                    report.Warning(Source, path + ".delayMs", "area delay is negative.", area.Id.ToString());
                }

                if (area.DurationMs <= 0)
                {
                    report.Error(Source, path + ".durationMs", "area duration must be positive.", area.Id.ToString());
                }

                if (area.MaxTargets < 0)
                {
                    report.Warning(Source, path + ".maxTargets", "area max targets is negative.", area.Id.ToString());
                }

                if (area.IntervalMs < 0)
                {
                    report.Warning(Source, path + ".intervalMs", "area interval is negative.", area.Id.ToString());
                }
            }
        }

        public static void ValidateSpawnAreaEffectiveTimings(
            TriggerPlanJsonDatabase triggers,
            Func<int, AoeMO> resolveArea,
            MobaRuntimeValidationReport report)
        {
            if (triggers == null || resolveArea == null || report == null) return;

            var records = triggers.Records;
            if (records == null) return;

            var spawnAreaId = TriggeringConstants.SpawnAreaId;
            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                var record = records[recordIndex];
                var actions = record.Plan.Actions;
                if (actions == null) continue;

                for (var actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                {
                    var action = actions[actionIndex];
                    if (!action.Id.Equals(spawnAreaId)) continue;

                    ValidateSpawnAreaEffectiveTiming(
                        in action,
                        record.TriggerId,
                        actionIndex,
                        resolveArea,
                        report);
                }
            }
        }

        private static void ValidateSpawnAreaEffectiveTiming(
            in ActionCallPlan action,
            int triggerId,
            int actionIndex,
            Func<int, AoeMO> resolveArea,
            MobaRuntimeValidationReport report)
        {
            if (!TryFindActionArg(
                    in action,
                    out var areaIdArg,
                    "area_id", "areaid", "aoe_id", "aoeid", "id")
                || !TryReadConstantInt(in areaIdArg, out var areaId)
                || areaId <= 0)
            {
                return;
            }

            var area = resolveArea(areaId);
            if (area == null
                || area.DelayMs <= 0
                || area.OnDelayTriggerIds == null
                || area.OnDelayTriggerIds.Length == 0)
            {
                return;
            }

            if (TryFindActionArg(
                    in action,
                    out var durationFramesArg,
                    "duration_frames", "durationframes", "lifetime_frames", "lifetimeframes"))
            {
                if (!TryReadConstantInt(in durationFramesArg, out var durationFrames)
                    || durationFrames > 0)
                {
                    return;
                }
            }

            var hasDurationOverride = TryFindActionArg(
                in action,
                out var durationMsArg,
                "duration_ms", "durationms", "lifetime_ms", "lifetimems");
            var durationOverrideMs = 0;
            if (hasDurationOverride
                && !TryReadConstantInt(in durationMsArg, out durationOverrideMs))
            {
                return;
            }

            var effectiveDurationMs = hasDurationOverride && durationOverrideMs > 0
                ? durationOverrideMs
                : area.DurationMs;
            if (effectiveDurationMs >= area.DelayMs) return;

            var path = $"trigger.{triggerId}.plan.actions[{actionIndex}].args.duration_ms";
            report.Error(
                Source,
                path,
                "spawn_area effective duration expires before its delayed trigger can execute. "
                + $"triggerId={triggerId}, actionIndex={actionIndex}, areaId={areaId}, "
                + $"configDurationMs={area.DurationMs}, "
                + $"overrideDurationMs={(hasDurationOverride ? durationOverrideMs : 0)}, "
                + $"effectiveDurationMs={effectiveDurationMs}, delayMs={area.DelayMs}",
                triggerId.ToString(),
                code: "moba.trigger.plan.spawn_area_effective_duration_before_delay",
                category: MobaRuntimeValidationCategory.Config,
                businessNumericId: triggerId);
        }

        private static bool TryFindActionArg(
            in ActionCallPlan action,
            out ActionArgValue value,
            params string[] aliases)
        {
            value = default;
            if (action.Args == null || action.Args.Count == 0) return false;

            foreach (var pair in action.Args)
            {
                for (var aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
                {
                    if (!string.Equals(
                            pair.Key,
                            aliases[aliasIndex],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    value = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadConstantInt(
            in ActionArgValue value,
            out int result)
        {
            result = 0;
            if (value.Ref.Kind != ENumericValueRefKind.Const
                || double.IsNaN(value.Ref.ConstValue)
                || double.IsInfinity(value.Ref.ConstValue))
            {
                return false;
            }

            var rounded = Math.Round(value.Ref.ConstValue);
            if (rounded < int.MinValue || rounded > int.MaxValue) return false;

            result = (int)rounded;
            return true;
        }

        private static void ValidateGameplay(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report)
        {
            foreach (var gameplay in All<GameplayMO>(config))
            {
                if (gameplay == null) continue;
                ValidateTriggerRefs(triggers, gameplay.TriggerIds, report, $"gameplay.{gameplay.Id}.triggerIds", gameplay.Id, TriggerPlanScope.Global);

                if (gameplay.DefaultDurationMs < 0)
                {
                    report.Warning(Source, $"gameplay.{gameplay.Id}.defaultDurationMs", "gameplay default duration is negative.", gameplay.Id.ToString());
                }
            }
        }

        private static void ValidateTagTemplates(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var template in All<TagTemplateMO>(config))
            {
                if (template == null) continue;
                var path = $"tagTemplate.{template.Id}";
                WarnEmptyName(template.Name, report, path + ".name", template.Id);
            }
        }

        private static void ValidateContinuousTagTemplates(MobaConfigDatabase config, MobaRuntimeValidationReport report)
        {
            foreach (var template in All<ContinuousTagTemplateMO>(config))
            {
                if (template == null) continue;
                var path = $"continuousTagTemplate.{template.Id}";
                WarnEmptyName(template.Name, report, path + ".name", template.Id);
            }
        }

        private static void ValidateSkillFlow(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report, SkillFlowMO flow, string path, int businessId)
        {
            if (flow == null) return;
            OptionalRef(Ref<ContinuousTagTemplateMO>(config.TryGetContinuousTagTemplate), flow.PipelineContinuousTagTemplateId, report, path + ".pipelineContinuousTagTemplateId", "continuous tag template", businessId);

            if (flow.Phases == null || flow.Phases.Count == 0)
            {
                report.Warning(Source, path + ".phases", "skill flow has no phases.", businessId.ToString());
                return;
            }

            for (int i = 0; i < flow.Phases.Count; i++)
            {
                ValidateSkillPhase(config, triggers, report, flow.Phases[i], $"{path}.phases[{i}]", businessId);
            }
        }

        private static void ValidateSkillPhase(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report, SkillPhaseDTO phase, string path, int businessId)
        {
            if (phase == null)
            {
                report.Warning(Source, path, "skill phase is null.", businessId.ToString());
                return;
            }

            switch ((SkillPhaseType)phase.Type)
            {
                case SkillPhaseType.Checks:
                    report.Error(Source, path + ".type", "checks skill phase is deprecated; use RulePlan trigger conditions instead.", businessId.ToString());
                    break;
                case SkillPhaseType.Timeline:
                    ValidateTimelinePhase(triggers, report, phase.Timeline, path + ".timeline", businessId);
                    break;
                case SkillPhaseType.Handlers:
                    report.Error(Source, path + ".type", "handlers skill phase is deprecated; use RulePlan trigger actions instead.", businessId.ToString());
                    break;
                case SkillPhaseType.RulePlan:
                    ValidateTriggerRefs(triggers, phase.RulePlan?.TriggerIds, report, path + ".rulePlan.triggerIds", businessId);
                    break;
                case SkillPhaseType.Sequence:
                case SkillPhaseType.Parallel:
                    ValidateChildPhases(config, triggers, report, phase.Children, path + ".children", businessId);
                    break;
                case SkillPhaseType.Repeat:
                    if (phase.Repeat == null)
                    {
                        report.Error(Source, path + ".repeat", "repeat phase has no repeat config.", businessId.ToString());
                    }
                    else
                    {
                        if (phase.Repeat.RepeatCount <= 0) report.Error(Source, path + ".repeat.repeatCount", "repeat count must be greater than zero.", businessId.ToString());
                        if (phase.Repeat.IntervalMs < 0) report.Error(Source, path + ".repeat.intervalMs", "repeat interval is negative.", businessId.ToString());
                        if (phase.Repeat.Phase == null) report.Error(Source, path + ".repeat.phase", "repeat phase has no explicit child phase.", businessId.ToString());
                        else ValidateSkillPhase(config, triggers, report, phase.Repeat.Phase, path + ".repeat.phase", businessId);
                    }
                    break;
                case SkillPhaseType.Delay:
                    if (phase.Delay == null) report.Error(Source, path + ".delay", "delay phase has no delay config.", businessId.ToString());
                    else if (phase.Delay.DelayMs < 0) report.Error(Source, path + ".delay.delayMs", "delay is negative.", businessId.ToString());
                    break;
                case SkillPhaseType.WaitUntil:
                    ValidateWaitUntilPhase(report, phase.WaitUntil, path + ".waitUntil", businessId);
                    break;
                default:
                    report.Warning(Source, path + ".type", "skill phase type is not recognized.", businessId.ToString());
                    break;
            }
        }

        private static void ValidateChildPhases(MobaConfigDatabase config, TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report, IReadOnlyList<SkillPhaseDTO> phases, string path, int businessId)
        {
            if (phases == null || phases.Count == 0)
            {
                report.Warning(Source, path, "composite skill phase has no children.", businessId.ToString());
                return;
            }

            for (int i = 0; i < phases.Count; i++)
            {
                ValidateSkillPhase(config, triggers, report, phases[i], $"{path}[{i}]", businessId);
            }
        }

        private static void ValidateWaitUntilPhase(MobaRuntimeValidationReport report, SkillWaitUntilPhaseDTO wait, string path, int businessId)
        {
            if (wait == null)
            {
                report.Error(Source, path, "waitUntil phase has no wait config.", businessId.ToString());
                return;
            }

            if (wait.TimeoutMs < 0) report.Error(Source, path + ".timeoutMs", "waitUntil timeout is negative.", businessId.ToString());
            if (!SkillWaitConditionCatalog.TryValidate(wait, out var error))
            {
                report.Error(Source, path + ".condition", error, businessId.ToString());
            }
        }

        private static void ValidateTimelinePhase(TriggerPlanJsonDatabase triggers, MobaRuntimeValidationReport report, SkillTimelinePhaseDTO timeline, string path, int businessId)
        {
            if (timeline == null)
            {
                report.Warning(Source, path, "timeline phase has no timeline config.", businessId.ToString());
                return;
            }

            if (timeline.DurationMs < 0)
            {
                report.Warning(Source, path + ".durationMs", "timeline duration is negative.", businessId.ToString());
            }

            var events = timeline.Events;
            if (events == null || events.Length == 0) return;

            for (int i = 0; i < events.Length; i++)
            {
                var item = events[i];
                if (item == null) continue;
                var itemPath = $"{path}.events[{i}]";
                if (item.AtMs < 0) report.Warning(Source, itemPath + ".atMs", "timeline event time is negative.", businessId.ToString());
                RequiredTriggerRef(triggers, item.EffectId, report, itemPath + ".effectId", businessId);
            }
        }

        private static void ValidateTriggerRefs(TriggerPlanJsonDatabase triggers, IReadOnlyList<int> ids, MobaRuntimeValidationReport report, string path, int businessId, TriggerPlanScope? expectedScope = null)
        {
            if (ids == null || ids.Count == 0) return;
            for (int i = 0; i < ids.Count; i++)
            {
                RequiredTriggerRef(triggers, ids[i], report, $"{path}[{i}]", businessId, expectedScope);
            }
        }

        private static void RequiredTriggerRef(TriggerPlanJsonDatabase triggers, int id, MobaRuntimeValidationReport report, string path, int businessId, TriggerPlanScope? expectedScope = null)
        {
            if (id <= 0)
            {
                report.Error(Source, path, "trigger id is empty.", businessId.ToString());
                return;
            }

            if (triggers == null)
            {
                report.Warning(Source, path, "TriggerPlanJsonDatabase is not resolved; trigger reference cannot be checked.", businessId.ToString());
                return;
            }

            if (!triggers.TryGetRecordByTriggerId(id, out var record))
            {
                report.Error(Source, path, $"trigger id '{id}' does not exist.", businessId.ToString());
                return;
            }

            if (expectedScope.HasValue && record.Scope != expectedScope.Value)
            {
                report.Error(Source, path, $"trigger id '{id}' scope is {record.Scope}; expected {expectedScope.Value}.", businessId.ToString());
            }
        }

        private static void ValidateRefs<T>(TryGetRef<T> tryGet, IReadOnlyList<int> ids, MobaRuntimeValidationReport report, string path, string label, int businessId) where T : class
        {
            if (ids == null || ids.Count == 0) return;
            for (int i = 0; i < ids.Count; i++)
            {
                RequiredRef(tryGet, ids[i], report, $"{path}[{i}]", label, businessId);
            }
        }

        private static void RequiredRef<T>(TryGetRef<T> tryGet, int id, MobaRuntimeValidationReport report, string path, string label, int businessId) where T : class
        {
            if (id <= 0)
            {
                report.Error(Source, path, label + " id is empty.", businessId.ToString());
                return;
            }

            if (tryGet == null || !tryGet(id, out var value) || value == null)
            {
                report.Error(Source, path, $"{label} id '{id}' does not exist.", businessId.ToString());
            }
        }

        private static void OptionalRef<T>(TryGetRef<T> tryGet, int id, MobaRuntimeValidationReport report, string path, string label, int businessId) where T : class
        {
            if (id <= 0) return;

            if (tryGet == null || !tryGet(id, out var value) || value == null)
            {
                report.Error(Source, path, $"{label} id '{id}' does not exist.", businessId.ToString());
            }
        }

        private static IEnumerable<T> All<T>(MobaConfigDatabase config) where T : class
        {
            if (config == null) return Array.Empty<T>();
            var table = config.GetTable<T>();
            return table != null ? table.All() : Array.Empty<T>();
        }

        private static bool TryGetTableRef<T>(MobaConfigDatabase config, int id, out T value) where T : class
        {
            value = null;
            if (config == null) return false;

            var table = config.GetTable<T>();
            return table != null && table.TryGet(id, out value);
        }

        private static void WarnEmptyName(string name, MobaRuntimeValidationReport report, string path, int businessId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                report.Warning(Source, path, "template name is empty.", businessId.ToString());
            }
        }

        private static TryGetRef<T> Ref<T>(TryGetRef<T> tryGet) where T : class
        {
            return tryGet;
        }

        private delegate bool TryGetRef<T>(int id, out T value) where T : class;
    }
}
