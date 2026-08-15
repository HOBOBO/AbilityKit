using System;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaDamagePipelineStage
    {
        string EventId { get; }
        void Execute(AttackCalcInfo calc);
    }

    public sealed class MobaBaseDamagePipelineStage : IMobaDamagePipelineStage
    {
        public string EventId => DamagePipelineEvents.AfterBase;

        public void Execute(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var attack = calc.Attack;
            var baseValue = attack.BaseDamage.FixedValue;
            var scaled = baseValue * attack.DamageRate.FixedValue + attack.FlatBonus.FixedValue;
            calc.RawDamage.FixedBaseValue = DeterministicMath.Max(Fixed64.Zero, scaled);
        }
    }

    public sealed class MobaDamageMitigationPipelineStage : IMobaDamagePipelineStage
    {
        private readonly MobaDamageMitigationService _mitigation;

        public MobaDamageMitigationPipelineStage(MobaDamageMitigationService mitigation)
        {
            _mitigation = mitigation;
        }

        public string EventId => DamagePipelineEvents.AfterMitigate;

        public void Execute(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var mitigated = _mitigation != null
                ? _mitigation.Mitigate(calc.Attack, calc.RawDamage.FixedValue)
                : calc.RawDamage.FixedValue;
            calc.MitigatedDamage.FixedBaseValue = DeterministicMath.Max(Fixed64.Zero, mitigated);
        }
    }

    public sealed class MobaShieldAbsorbPipelineStage : IMobaDamagePipelineStage
    {
        private readonly MobaShieldService _shields;

        public MobaShieldAbsorbPipelineStage(MobaShieldService shields)
        {
            _shields = shields;
        }

        public string EventId => DamagePipelineEvents.AfterShield;

        public void Execute(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            calc.ShieldPlan = _shields?.PreviewAbsorb(calc.Attack, calc.MitigatedDamage.FixedValue);
            var shieldAbsorb = calc.ShieldPlan != null ? calc.ShieldPlan.Absorbed : Fixed64.Zero;
            calc.ShieldAbsorb.FixedBaseValue = DeterministicMath.Max(Fixed64.Zero, shieldAbsorb);
            calc.HpDamage.FixedBaseValue = DeterministicMath.Max(Fixed64.Zero, calc.MitigatedDamage.FixedValue - calc.ShieldAbsorb.FixedValue);
        }
    }

    public sealed class MobaFinalDamagePipelineStage : IMobaDamagePipelineStage
    {
        public string EventId => DamagePipelineEvents.CalcFinal;

        public void Execute(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var finalOverride = calc.Attack.FinalDamage.FixedValue;
            if (finalOverride > Fixed64.Zero)
            {
                calc.HpDamage.FixedBaseValue = finalOverride;
            }
        }
    }
}
