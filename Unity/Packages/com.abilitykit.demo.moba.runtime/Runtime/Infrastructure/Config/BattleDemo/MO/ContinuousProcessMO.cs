using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ContinuousProcessMO
    {
        public int Id { get; }
        public string Name { get; }
        public int DurationMs { get; }
        public int IntervalMs { get; }
        public IReadOnlyList<int> IntervalTriggerIds { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public int ContinuousTagTemplateId { get; }
        public GameplayTagContainer Tags { get; }
        public IReadOnlyList<ContinuousModifierMO> Modifiers { get; }
        public bool RequireOutOfCombat { get; }
        public int OutOfCombatSeconds { get; }

        public ContinuousProcessMO(ContinuousProcessDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;
            DurationMs = dto.DurationMs;
            IntervalMs = dto.IntervalMs;
            IntervalTriggerIds = dto.IntervalTriggerIds ?? Array.Empty<int>();
            TriggerIds = dto.TriggerIds ?? Array.Empty<int>();
            ContinuousTagTemplateId = dto.ContinuousTagTemplateId;
            Tags = MobaGameplayTagCatalog.ToConfigContainer(dto.TagNames);
            Modifiers = CreateModifiers(dto.Modifiers);
            RequireOutOfCombat = dto.RequireOutOfCombat;
            OutOfCombatSeconds = dto.OutOfCombatSeconds;
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
