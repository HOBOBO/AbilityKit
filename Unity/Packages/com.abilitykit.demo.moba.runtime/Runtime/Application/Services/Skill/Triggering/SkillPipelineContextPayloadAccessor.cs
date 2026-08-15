using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Demo.Moba.Services
{
    public static class SkillRulePayloadFields
    {
        private const string PayloadPrefix = "payload:";
        private const string LegacyFieldPrefix = "field:";

        public const string SkillId = "skill.id";
        public const string SkillSlot = "skill.slot";
        public const string SkillLevel = "skill.level";
        public const string SkillCost = "skill.cost";
        public const string SkillCooldownMs = "skill.cooldown_ms";
        public const string SkillCooldownRemainingMs = "skill.cooldown_remaining_ms";
        public const string CasterActorId = "caster.actor_id";
        public const string TargetActorId = "target.actor_id";
        public const string CasterMana = "caster.mana";
        public const string CasterManaMax = "caster.mana.max";
        public const string CasterManaPercent = "caster.mana.percent";
        public const string CasterResourceMana = "caster.resource.2";
        public const string CasterResourceManaMax = "caster.resource.2.max";

        public static int FieldId(string name)
        {
            return string.IsNullOrEmpty(name) ? 0 : StableStringId.Get(PayloadPrefix + name);
        }

        public static int LegacyFieldId(string name)
        {
            return string.IsNullOrEmpty(name) ? 0 : StableStringId.Get(LegacyFieldPrefix + name);
        }
    }

    [GeneratePayloadFieldIds(
        typeof(SkillRulePayloadFields),
        "SupportsField",
        true,
        nameof(SkillRulePayloadFields.SkillId),
        nameof(SkillRulePayloadFields.SkillSlot),
        nameof(SkillRulePayloadFields.SkillLevel),
        nameof(SkillRulePayloadFields.SkillCost),
        nameof(SkillRulePayloadFields.SkillCooldownMs),
        nameof(SkillRulePayloadFields.SkillCooldownRemainingMs),
        nameof(SkillRulePayloadFields.CasterActorId),
        nameof(SkillRulePayloadFields.TargetActorId),
        nameof(SkillRulePayloadFields.CasterMana),
        nameof(SkillRulePayloadFields.CasterManaMax),
        nameof(SkillRulePayloadFields.CasterManaPercent),
        nameof(SkillRulePayloadFields.CasterResourceMana),
        nameof(SkillRulePayloadFields.CasterResourceManaMax))]
    public sealed partial class SkillPipelineContextPayloadAccessor : IPayloadIntAccessor<SkillPipelineContext>, IPayloadDoubleAccessor<SkillPipelineContext>
    {
        private readonly IWorldResolver _services;
        private MobaConfigDatabase _configs;
        private MobaActorLookupService _actors;
        private IFrameTime _time;

        public SkillPipelineContextPayloadAccessor(MobaConfigDatabase configs, MobaActorLookupService actors, IFrameTime time = null)
        {
            _configs = configs;
            _actors = actors;
            _time = time;
        }

        public SkillPipelineContextPayloadAccessor(IWorldResolver services)
        {
            _services = services;
        }

        public bool TryGet(in SkillPipelineContext args, int fieldId, out int value)
        {
            value = 0;
            if (args == null) return false;

            if (fieldId == SkillIdId || fieldId == SkillIdLegacyId)
            {
                value = args.SkillId;
                return true;
            }

            if (fieldId == SkillSlotId || fieldId == SkillSlotLegacyId)
            {
                value = args.SkillSlot;
                return true;
            }

            if (fieldId == SkillLevelId || fieldId == SkillLevelLegacyId)
            {
                value = Math.Max(1, args.GetSkillLevel());
                return true;
            }

            if (fieldId == CasterActorIdId || fieldId == CasterActorIdLegacyId)
            {
                value = args.CasterActorId;
                return true;
            }

            if (fieldId == TargetActorIdId || fieldId == TargetActorIdLegacyId)
            {
                value = args.TargetActorId;
                return true;
            }

            if (fieldId == SkillCostId || fieldId == SkillCostLegacyId || fieldId == SkillCooldownMsId || fieldId == SkillCooldownMsLegacyId || fieldId == SkillCooldownRemainingMsId || fieldId == SkillCooldownRemainingMsLegacyId || fieldId == CasterManaId || fieldId == CasterManaLegacyId || fieldId == CasterManaMaxId || fieldId == CasterManaMaxLegacyId || fieldId == CasterManaPercentId || fieldId == CasterManaPercentLegacyId || fieldId == CasterResourceManaId || fieldId == CasterResourceManaLegacyId || fieldId == CasterResourceManaMaxId || fieldId == CasterResourceManaMaxLegacyId)
            {
                double doubleValue;
                if (!TryGet(in args, fieldId, out doubleValue)) return false;
                value = (int)Math.Round(doubleValue);
                return true;
            }

            return false;
        }

        public bool TryGet(in SkillPipelineContext args, int fieldId, out double value)
        {
            value = 0d;
            if (args == null) return false;

            if (fieldId == SkillCostId || fieldId == SkillCostLegacyId)
            {
                value = args.ResolvedConfiguration.ResourceCost;
                return args.ResolvedConfiguration.IsValid;
            }

            if (fieldId == SkillCooldownMsId || fieldId == SkillCooldownMsLegacyId)
            {
                value = args.ResolvedConfiguration.CooldownMs;
                return args.ResolvedConfiguration.IsValid;
            }

            if (fieldId == SkillCooldownRemainingMsId || fieldId == SkillCooldownRemainingMsLegacyId)
            {
                value = GetCooldownRemainingMs(args);
                return true;
            }

            if (fieldId == CasterManaId || fieldId == CasterManaLegacyId || fieldId == CasterResourceManaId || fieldId == CasterResourceManaLegacyId)
            {
                return TryGetResource(args, args.CasterActorId, ResourceType.Mana, out value, out _);
            }

            if (fieldId == CasterManaMaxId || fieldId == CasterManaMaxLegacyId || fieldId == CasterResourceManaMaxId || fieldId == CasterResourceManaMaxLegacyId)
            {
                return TryGetResource(args, args.CasterActorId, ResourceType.Mana, out _, out value);
            }

            if (fieldId == CasterManaPercentId || fieldId == CasterManaPercentLegacyId)
            {
                if (!TryGetResource(args, args.CasterActorId, ResourceType.Mana, out var current, out var max) || max <= 0d) return false;
                value = current / max;
                return true;
            }

            if (fieldId == SkillIdId || fieldId == SkillIdLegacyId)
            {
                value = args.SkillId;
                return true;
            }

            if (fieldId == SkillSlotId || fieldId == SkillSlotLegacyId)
            {
                value = args.SkillSlot;
                return true;
            }

            if (fieldId == SkillLevelId || fieldId == SkillLevelLegacyId)
            {
                value = Math.Max(1, args.GetSkillLevel());
                return true;
            }

            if (fieldId == CasterActorIdId || fieldId == CasterActorIdLegacyId)
            {
                value = args.CasterActorId;
                return true;
            }

            if (fieldId == TargetActorIdId || fieldId == TargetActorIdLegacyId)
            {
                value = args.TargetActorId;
                return true;
            }

            return false;
        }

        private bool TryGetResource(SkillPipelineContext context, int actorId, ResourceType resourceType, out double current, out double max)
        {
            current = 0d;
            max = 0d;
            var actors = ResolveActors(context);
            if (actorId <= 0 || resourceType == ResourceType.None || actors == null) return false;
            if (!actors.TryGetActorEntity(actorId, out var actor) || actor == null) return false;
            if (!actor.hasResourceContainer || actor.resourceContainer.Value == null || actor.resourceContainer.Value.Map == null) return false;
            if (!actor.resourceContainer.Value.Map.TryGetValue(resourceType, out var state) || state == null) return false;

            current = MobaResourceFixedConvert.ToSingle(state.Current);
            max = ResolveResourceMax(actor, state, resourceType);
            return true;
        }

        private double GetCooldownRemainingMs(SkillPipelineContext context)
        {
            var actors = ResolveActors(context);
            if (context == null || actors == null) return 0d;
            if (!MobaSkillRuntimeAccess.TryGetActiveSkill(actors, context.CasterActorId, context.SkillSlot, context.SkillId, out var runtime) || runtime == null) return 0d;

            var now = MobaSkillRuntimeAccess.GetCurrentTimeMs(ResolveTime(context));
            return Math.Max(0d, runtime.CooldownEndTimeMs - now);
        }

        private MobaConfigDatabase ResolveConfigs(SkillPipelineContext context)
        {
            if (context != null && context.WorldServices != null && context.WorldServices.TryResolve<MobaConfigDatabase>(out var configs) && configs != null)
            {
                _configs = configs;
                return configs;
            }

            if (_configs == null && _services != null)
            {
                _services.TryResolve(out _configs);
            }

            return _configs;
        }

        private MobaActorLookupService ResolveActors(SkillPipelineContext context)
        {
            if (context != null && context.WorldServices != null && context.WorldServices.TryResolve<MobaActorLookupService>(out var actors) && actors != null)
            {
                _actors = actors;
                return actors;
            }

            if (_actors == null && _services != null)
            {
                _services.TryResolve(out _actors);
            }

            return _actors;
        }

        private IFrameTime ResolveTime(SkillPipelineContext context)
        {
            if (context != null && context.WorldServices != null && context.WorldServices.TryResolve<IFrameTime>(out var time) && time != null)
            {
                _time = time;
                return time;
            }

            if (_time == null && _services != null)
            {
                _services.TryResolve(out _time);
            }

            return _time;
        }

        private static double ResolveResourceMax(global::ActorEntity actor, ResourceState state, ResourceType resourceType)
        {
            if (state != null && state.LastMax > AbilityKit.Deterministic.Fixed64.Zero) return MobaResourceFixedConvert.ToSingle(state.LastMax);
            if (actor == null || !actor.hasAttributeGroup || actor.attributeGroup.Group == null) return 0d;

            var attr = resourceType switch
            {
                ResourceType.Hp => BattleAttributeType.MAX_HP,
                ResourceType.Mana => BattleAttributeType.MAX_MANA,
                _ => BattleAttributeType.None
            };

            return attr != BattleAttributeType.None
                ? actor.attributeGroup.Group.GetValue(MobaAttributeIds.Get(attr))
                : 0d;
        }
    }

    [GeneratePayloadFieldIds(
        typeof(SkillRulePayloadFields),
        "SupportsField",
        true,
        nameof(SkillRulePayloadFields.SkillId),
        nameof(SkillRulePayloadFields.SkillSlot),
        nameof(SkillRulePayloadFields.SkillLevel),
        nameof(SkillRulePayloadFields.CasterActorId),
        nameof(SkillRulePayloadFields.TargetActorId))]
    public sealed partial class SkillCastContextPayloadAccessor : IPayloadIntAccessor<SkillCastContext>, IPayloadDoubleAccessor<SkillCastContext>
    {
        public bool TryGet(in SkillCastContext args, int fieldId, out int value)
        {
            value = 0;
            if (args == null) return false;

            if (fieldId == SkillIdId || fieldId == SkillIdLegacyId)
            {
                value = args.SkillId;
                return true;
            }

            if (fieldId == SkillSlotId || fieldId == SkillSlotLegacyId)
            {
                value = args.SkillSlot;
                return true;
            }

            if (fieldId == SkillLevelId || fieldId == SkillLevelLegacyId)
            {
                value = Math.Max(1, args.SkillLevel);
                return true;
            }

            if (fieldId == CasterActorIdId || fieldId == CasterActorIdLegacyId)
            {
                value = args.CasterActorId;
                return true;
            }

            if (fieldId == TargetActorIdId || fieldId == TargetActorIdLegacyId)
            {
                value = args.TargetActorId;
                return true;
            }

            return false;
        }

        public bool TryGet(in SkillCastContext args, int fieldId, out double value)
        {
            int intValue;
            if (TryGet(in args, fieldId, out intValue))
            {
                value = intValue;
                return true;
            }

            value = 0d;
            return false;
        }
    }

    public sealed class SkillPipelineContextObjectPayloadAccessor : IPayloadIntAccessor<object>, IPayloadDoubleAccessor<object>
    {
        private readonly SkillPipelineContextPayloadAccessor _inner;

        public SkillPipelineContextObjectPayloadAccessor(SkillPipelineContextPayloadAccessor inner)
        {
            _inner = inner;
        }

        public static bool SupportsField(int fieldId)
        {
            return SkillPipelineContextPayloadAccessor.SupportsField(fieldId);
        }

        public bool TryGet(in object args, int fieldId, out int value)
        {
            if (_inner != null && args is SkillPipelineContext context)
            {
                return _inner.TryGet(in context, fieldId, out value);
            }

            value = default;
            return false;
        }

        public bool TryGet(in object args, int fieldId, out double value)
        {
            if (_inner != null && args is SkillPipelineContext context)
            {
                return _inner.TryGet(in context, fieldId, out value);
            }

            value = default;
            return false;
        }
    }
}
