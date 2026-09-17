using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IBattleDiagnosticDefinitionCatalogSnapshotSource
    {
        BattleDiagnosticDefinitionCatalogSnapshot CaptureDefinitionCatalogSnapshot(
            BattleDiagnosticSessionScope scope,
            IReadOnlyList<BattleDiagnosticDefinitionReference> references);
    }

    public interface IMobaBattleDiagnosticDefinitionResolver
    {
        long Revision { get; }

        bool TryResolve(
            in BattleDiagnosticDefinitionReference reference,
            out BattleDiagnosticDefinition definition);
    }

    [WorldService(typeof(IBattleDiagnosticDefinitionCatalogSnapshotSource), WorldLifetime.Scoped, isDefault: false, profile: WorldServiceProfile.Client | WorldServiceProfile.Server)]
    [WorldService(typeof(IMobaBattleDiagnosticDefinitionResolver), WorldLifetime.Scoped, isDefault: false, profile: WorldServiceProfile.Client | WorldServiceProfile.Server)]
    [WorldService(typeof(MobaBattleDiagnosticDefinitionCatalogSource), WorldLifetime.Scoped, isDefault: false, profile: WorldServiceProfile.Client | WorldServiceProfile.Server)]
    public sealed class MobaBattleDiagnosticDefinitionCatalogSource :
        IBattleDiagnosticDefinitionCatalogSnapshotSource,
        IMobaBattleDiagnosticDefinitionResolver,
        IService
    {
        private readonly MobaConfigDatabase _configs;
        private readonly TriggerPlanJsonDatabase _triggers;

        [WorldInject(required: false)]
        private PlanActionModuleRegistry _actionModules = null;

        public MobaBattleDiagnosticDefinitionCatalogSource(
            MobaConfigDatabase configs,
            TriggerPlanJsonDatabase triggers)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        }

        public long Revision => Math.Max(0L, _configs.Version);

        public BattleDiagnosticDefinitionCatalogSnapshot CaptureDefinitionCatalogSnapshot(
            BattleDiagnosticSessionScope scope,
            IReadOnlyList<BattleDiagnosticDefinitionReference> references)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid scope is required.", nameof(scope));
            var items = new List<BattleDiagnosticDefinition>(references?.Count ?? 0);
            var seen = new HashSet<BattleDiagnosticDefinitionReference>();
            if (references != null)
            {
                for (var i = 0; i < references.Count; i++)
                {
                    var reference = references[i];
                    if (!reference.HasDefinitionId || !seen.Add(reference)) continue;
                    items.Add(TryResolve(in reference, out var definition)
                        ? definition
                        : BattleDiagnosticDefinition.Unresolved(in reference));
                }
            }

            items.Sort(CompareDefinitions);
            return new BattleDiagnosticDefinitionCatalogSnapshot(
                scope,
                Revision,
                items);
        }

        public bool TryResolve(
            in BattleDiagnosticDefinitionReference reference,
            out BattleDiagnosticDefinition definition)
        {
            switch (reference.Kind)
            {
                case BattleDiagnosticDefinitionKind.Action:
                    if (_actionModules != null)
                    {
                        var descriptors = _actionModules.Descriptors;
                        for (var i = 0; i < descriptors.Length; i++)
                        {
                            var descriptor = descriptors[i];
                            if (string.IsNullOrEmpty(descriptor.ActionName) ||
                                PlanActionRegisterUtil.GetActionId(descriptor.ActionName).Value !=
                                reference.DefinitionId) continue;
                            definition = Resolved(
                                in reference,
                                descriptor.ActionName,
                                "plan-action-modules",
                                BattleDiagnosticDefinitionMetadataEntry.String("actionName", descriptor.ActionName),
                                BattleDiagnosticDefinitionMetadataEntry.String("moduleName", descriptor.ModuleName));
                            return true;
                        }
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Actor:
                    if (_configs.TryGetCharacter(reference.DefinitionId, out var actor))
                    {
                        definition = Resolved(
                            in reference,
                            actor.Name,
                            "moba.characters",
                            Meta("modelId", actor.ModelId),
                            Meta("attributeTemplateId", actor.AttributeTemplateId),
                            Meta("skillCount", actor.SkillIds.Count),
                            Meta("passiveSkillCount", actor.PassiveSkillIds.Count));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Skill:
                    if (_configs.TryGetSkill(reference.DefinitionId, out var skill))
                    {
                        definition = Resolved(
                            in reference,
                            skill.Name,
                            "moba.skills",
                            Meta("cooldownMs", skill.CooldownMs),
                            Meta("range", skill.Range),
                            Meta("category", skill.Category),
                            Meta("skillType", (int)skill.SkillType),
                            Meta("preCastFlowId", skill.PreCastFlowId),
                            Meta("castFlowId", skill.CastFlowId));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Buff:
                    if (_configs.TryGetBuff(reference.DefinitionId, out var buff))
                    {
                        definition = Resolved(
                            in reference,
                            buff.Name,
                            "moba.buffs",
                            Meta("durationMs", buff.DurationMs),
                            Meta("intervalMs", buff.IntervalMs),
                            Meta("maxStacks", buff.MaxStacks),
                            Meta("stackingPolicy", (int)buff.StackingPolicy),
                            Meta("refreshPolicy", (int)buff.RefreshPolicy),
                            Meta("triggerCount", buff.TriggerIds.Count));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Projectile:
                    if (_configs.TryGetProjectile(reference.DefinitionId, out var projectile))
                    {
                        definition = Resolved(
                            in reference,
                            projectile.Name,
                            "moba.projectiles",
                            Meta("speed", projectile.Speed),
                            Meta("lifetimeMs", projectile.LifetimeMs),
                            Meta("maxDistance", projectile.MaxDistance),
                            Meta("hitPolicy", (int)projectile.HitPolicyKind),
                            Meta("onHitEffectId", projectile.OnHitEffectId));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Area:
                    if (_configs.TryGetAoe(reference.DefinitionId, out var area))
                    {
                        definition = Resolved(
                            in reference,
                            area.Name,
                            "moba.areas",
                            Meta("radius", area.Radius),
                            Meta("delayMs", area.DelayMs),
                            Meta("durationMs", area.DurationMs),
                            Meta("intervalMs", area.IntervalMs),
                            Meta("maxTargets", area.MaxTargets));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Summon:
                    if (_configs.TryGetSummon(reference.DefinitionId, out var summon))
                    {
                        definition = Resolved(
                            in reference,
                            summon.Name,
                            "moba.summons",
                            Meta("unitSubType", summon.UnitSubType),
                            Meta("modelId", summon.ModelId),
                            Meta("lifetimeMs", summon.LifetimeMs),
                            Meta("maxAlivePerOwner", summon.MaxAlivePerOwner),
                            Meta("despawnOnOwnerDie", summon.DespawnOnOwnerDie));
                        return true;
                    }
                    break;
                case BattleDiagnosticDefinitionKind.Trigger:
                case BattleDiagnosticDefinitionKind.Effect:
                    if (_triggers.TryGetRecordByTriggerId(reference.DefinitionId, out var trigger))
                    {
                        var fallbackName = reference.Kind + " " + reference.DefinitionId;
                        definition = Resolved(
                            in reference,
                            string.IsNullOrEmpty(trigger.EventName) ? fallbackName : trigger.EventName,
                            "trigger-plans",
                            BattleDiagnosticDefinitionMetadataEntry.String(
                                "eventName",
                                trigger.EventName),
                            Meta("eventId", trigger.EventId),
                            Meta("scope", (int)trigger.Scope));
                        return true;
                    }
                    break;
            }

            definition = null;
            return false;
        }

        private BattleDiagnosticDefinition Resolved(
            in BattleDiagnosticDefinitionReference reference,
            string displayName,
            string sourcePath,
            params BattleDiagnosticDefinitionMetadataEntry[] metadata)
        {
            var revision = reference.Kind == BattleDiagnosticDefinitionKind.Trigger ||
                           reference.Kind == BattleDiagnosticDefinitionKind.Effect
                ? string.Empty
                : _configs.Version.ToString(CultureInfo.InvariantCulture);
            return new BattleDiagnosticDefinition(
                in reference,
                displayName,
                revision,
                ComputeContentHash(reference, displayName, metadata),
                sourcePath,
                BattleDiagnosticDefinitionResolution.Resolved,
                metadata);
        }

        private static BattleDiagnosticDefinitionMetadataEntry Meta(string key, int value) =>
            BattleDiagnosticDefinitionMetadataEntry.Integer(key, value);

        private static BattleDiagnosticDefinitionMetadataEntry Meta(string key, float value) =>
            BattleDiagnosticDefinitionMetadataEntry.Number(key, value);

        private static BattleDiagnosticDefinitionMetadataEntry Meta(string key, bool value) =>
            BattleDiagnosticDefinitionMetadataEntry.Boolean(key, value);

        private static int CompareDefinitions(
            BattleDiagnosticDefinition left,
            BattleDiagnosticDefinition right)
        {
            var kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : left.DefinitionId.CompareTo(right.DefinitionId);
        }

        private static string ComputeContentHash(
            BattleDiagnosticDefinitionReference reference,
            string displayName,
            IReadOnlyList<BattleDiagnosticDefinitionMetadataEntry> metadata)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            Add(ref hash, ((int)reference.Kind).ToString(CultureInfo.InvariantCulture), prime);
            Add(ref hash, reference.DefinitionId.ToString(CultureInfo.InvariantCulture), prime);
            Add(ref hash, displayName, prime);
            for (var i = 0; i < metadata.Count; i++)
            {
                var item = metadata[i];
                Add(ref hash, item.Key, prime);
                Add(ref hash, ((int)item.ValueKind).ToString(CultureInfo.InvariantCulture), prime);
                Add(ref hash, item.StringValue, prime);
                Add(ref hash, item.IntegerValue.ToString(CultureInfo.InvariantCulture), prime);
                Add(ref hash, item.NumberValue.ToString("R", CultureInfo.InvariantCulture), prime);
                Add(ref hash, item.BooleanValue ? "1" : "0", prime);
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void Add(ref ulong hash, string value, ulong prime)
        {
            value = value ?? string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= prime;
            }
            hash ^= 0xff;
            hash *= prime;
        }

        public void Dispose()
        {
        }
    }
}
