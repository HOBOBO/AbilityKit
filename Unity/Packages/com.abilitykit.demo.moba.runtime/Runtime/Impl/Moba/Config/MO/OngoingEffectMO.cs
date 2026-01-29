using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class OngoingEffectMO
    {
        public int Id { get; }
        public string Name { get; }

        public int DurationMs { get; }
        public int PeriodMs { get; }

        public int OnApplyEffectId { get; }
        public int OnTickEffectId { get; }
        public int OnRemoveEffectId { get; }

        public OngoingEffectMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.OngoingEffectDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;

            DurationMs = dto.DurationMs;
            PeriodMs = dto.PeriodMs;

            OnApplyEffectId = dto.OnApplyEffectId;
            OnTickEffectId = dto.OnTickEffectId;
            OnRemoveEffectId = dto.OnRemoveEffectId;
        }
    }
}
