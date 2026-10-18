using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ContinuousModifierMO : IMobaContinuousModifierSpec,
        IMobaContinuousParameterSelectorSpec, IMobaContinuousSkillCandidateSpec,
        IMobaContinuousReferenceCandidateSpec, IMobaContinuousFormulaSpec
    {
        public ContinuousModifierMO(ContinuousModifierDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            DefinitionId = dto.DefinitionId;
            TargetKind = dto.TargetKind;
            TargetId = dto.TargetId;
            Op = dto.Op;
            Value = dto.Value;
            Magnitude = CreateMagnitude(dto);
            EvaluationPolicy = dto.EvaluationPolicy;
            AttributeSourceRole = dto.AttributeSourceRole;
            Priority = dto.Priority;
            CandidateSkillId = dto.CandidateSkillId;
            CandidateReferenceId = dto.CandidateReferenceId;
            HasFormula = dto.MagnitudeSourceType == 6;
            FormulaConstant = dto.FormulaConstant;
            FormulaLevelCoefficient = dto.FormulaLevelCoefficient;
            FormulaTerms = dto.FormulaTerms ?? Array.Empty<ModifierFormulaTermDTO>();
            Selector = new MobaParameterSelector(dto.SelectorSlot, dto.SelectorBaseSkillId,
                dto.SelectorEffectiveSkillId, dto.SelectorLauncherTemplateId,
                dto.SelectorProjectileTemplateId, dto.SelectorAreaTemplateId,
                dto.SelectorSummonTemplateId, dto.SelectorBuffTemplateId);
        }

        public int DefinitionId { get; }
        public int TargetKind { get; }
        public int TargetId { get; }
        public int Op { get; }
        public float Value { get; }
        public MagnitudeSource Magnitude { get; }
        public int EvaluationPolicy { get; }
        public int AttributeSourceRole { get; }
        public int Priority { get; }
        public int CandidateSkillId { get; }
        public int CandidateReferenceId { get; }
        public bool HasFormula { get; }
        public float FormulaConstant { get; }
        public float FormulaLevelCoefficient { get; }
        public IReadOnlyList<ModifierFormulaTermDTO> FormulaTerms { get; }
        public MobaParameterSelector Selector { get; }

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
                    return MagnitudeSource.Attribute(CreateAttributeKey(dto.MagnitudeAttributeTypeId), coefficient);
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
            var attribute = MobaAttributeIds.Get((BattleAttributeType)attrTypeId);
            return attribute.IsValid ? ModifierKey.FromPacked((uint)attribute.RawId) : ModifierKey.None;
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
            Tags = MobaGameplayTagCatalog.ToConfigContainer(dto.TagNames);
            Modifiers = CreateModifiers(dto.Modifiers);
            DispelPolicy = (BuffDispelPolicy)dto.DispelPolicy;
            DispelCategory = dto.DispelCategory;
            DispelBlockedByTags = MobaGameplayTagCatalog.ToConfigContainer(dto.DispelBlockedByTagNames);
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
