using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ContinuousModifierMO : IMobaContinuousModifierSpec
    {
        public ContinuousModifierMO(ContinuousModifierDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            TargetKind = dto.TargetKind != 0 ? dto.TargetKind : MobaContinuousModifierTargetKind.Attribute;
            TargetId = dto.TargetId != 0 ? dto.TargetId : dto.AttrTypeId;
            Op = dto.Op;
            Value = dto.Value;
            Magnitude = CreateMagnitude(dto);
            EvaluationPolicy = dto.EvaluationPolicy;
            Priority = dto.Priority;
        }

        public int TargetKind { get; }
        public int TargetId { get; }
        public int Op { get; }
        public float Value { get; }
        public MagnitudeSource Magnitude { get; }
        public int EvaluationPolicy { get; }
        public int Priority { get; }

        private static MagnitudeSource CreateMagnitude(ContinuousModifierDTO dto)
        {
            var sourceType = (MagnitudeSourceType)dto.MagnitudeSourceType;
            var baseValue = dto.MagnitudeBaseValue != 0f ? dto.MagnitudeBaseValue : dto.Value;
            var coefficient = dto.MagnitudeCoefficient != 0f ? dto.MagnitudeCoefficient : 1f;

            switch (sourceType)
            {
                case MagnitudeSourceType.Scalable:
                    return MagnitudeSource.LevelCurve(baseValue, dto.MagnitudeCurve, coefficient);
                case MagnitudeSourceType.Attribute:
                    var attrId = dto.MagnitudeAttributeTypeId != 0 ? dto.MagnitudeAttributeTypeId : dto.AttrTypeId;
                    return MagnitudeSource.Attribute(CreateAttributeKey(attrId), coefficient);
                case MagnitudeSourceType.TimeDecay:
                    return dto.MagnitudeCurve != null && dto.MagnitudeCurve.Length > 0
                        ? MagnitudeSource.TimeDecay(baseValue, dto.MagnitudeDuration, dto.MagnitudeCurve)
                        : MagnitudeSource.TimeDecay(baseValue, dto.MagnitudeDuration, (DecayType)dto.MagnitudeDecayType);
                case MagnitudeSourceType.ContextFloat:
                    return MagnitudeSource.ContextFloat(dto.MagnitudeContextKey, coefficient, baseValue);
                case MagnitudeSourceType.Pipeline:
                    return MagnitudeSource.Fixed(baseValue);
                default:
                    return MagnitudeSource.Fixed(dto.Value);
            }
        }

        private static ModifierKey CreateAttributeKey(int attrTypeId)
        {
            return ModifierKey.Create(ModifierKey.Categories.Attribute, ToByte(attrTypeId));
        }

        private static byte ToByte(int value)
        {
            if (value <= 0) return 0;
            if (value >= byte.MaxValue) return byte.MaxValue;
            return (byte)value;
        }
    }

    public enum BuffDispelPolicy
    {
        LegacyTag = 0,
        Dispellable = 1,
        Undispellable = 2,
    }

    public sealed class BuffMO
    {
        public int Id { get; }
        public string Name { get; }
        public int DurationMs { get; }

        public IReadOnlyList<int> OnAddEffects { get; }
        public IReadOnlyList<int> OnRemoveEffects { get; }
        public IReadOnlyList<int> OnIntervalEffects { get; }
        public int IntervalMs { get; }
        public int PresentationTemplateId { get; }
        public BuffStackingPolicy StackingPolicy { get; }
        public BuffRefreshPolicy RefreshPolicy { get; }
        public int MaxStacks { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public int ContinuousTagTemplateId { get; }
        public GameplayTagContainer Tags { get; }
        public IReadOnlyList<ContinuousModifierMO> Modifiers { get; }
        public BuffDispelPolicy DispelPolicy { get; }
        public int DispelCategory { get; }
        public GameplayTagContainer DispelBlockedByTags { get; }

        public BuffMO(BuffDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            DurationMs = dto.DurationMs;

            OnAddEffects = dto.OnAddEffects ?? Array.Empty<int>();
            OnRemoveEffects = dto.OnRemoveEffects ?? Array.Empty<int>();
            OnIntervalEffects = dto.OnIntervalEffects ?? Array.Empty<int>();
            IntervalMs = dto.IntervalMs;
            PresentationTemplateId = dto.PresentationTemplateId;
            StackingPolicy = (BuffStackingPolicy)dto.StackingPolicy;
            RefreshPolicy = (BuffRefreshPolicy)dto.RefreshPolicy;
            MaxStacks = dto.MaxStacks;
            TriggerIds = dto.TriggerIds ?? Array.Empty<int>();
            ContinuousTagTemplateId = dto.ContinuousTagTemplateId;
            Tags = MobaGameplayTagCatalog.ToContainer(dto.TagNames);
            Modifiers = CreateModifiers(dto.Modifiers);
            DispelPolicy = (BuffDispelPolicy)dto.DispelPolicy;
            DispelCategory = dto.DispelCategory;
            DispelBlockedByTags = MobaGameplayTagCatalog.ToContainer(dto.DispelBlockedByTagNames);
        }

        private static IReadOnlyList<ContinuousModifierMO> CreateModifiers(ContinuousModifierDTO[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0) return Array.Empty<ContinuousModifierMO>();

            var list = new ContinuousModifierMO[modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++)
            {
                list[i] = new ContinuousModifierMO(modifiers[i]);
            }

            return list;
        }
    }
}
