using System;
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
            var baseValue = attack.BaseDamage.Value;
            var scaled = baseValue * attack.DamageRate.Value + attack.FlatBonus.Value;
            calc.RawDamage.BaseValue = Math.Max(0f, scaled);
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
                ? _mitigation.Mitigate(calc.Attack, calc.RawDamage.Value)
                : calc.RawDamage.Value;
            calc.MitigatedDamage.BaseValue = Math.Max(0f, mitigated);
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

            calc.ShieldPlan = _shields?.PreviewAbsorb(calc.Attack, calc.MitigatedDamage.Value);
            var shieldAbsorb = calc.ShieldPlan != null ? calc.ShieldPlan.Absorbed : 0f;
            calc.ShieldAbsorb.BaseValue = Math.Max(0f, shieldAbsorb);
            calc.HpDamage.BaseValue = Math.Max(0f, calc.MitigatedDamage.Value - calc.ShieldAbsorb.Value);
        }
    }

    public sealed class MobaFinalDamagePipelineStage : IMobaDamagePipelineStage
    {
        public string EventId => DamagePipelineEvents.CalcFinal;

        public void Execute(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var finalOverride = calc.Attack.FinalDamage.Value;
            if (finalOverride > 0f)
            {
                calc.HpDamage.BaseValue = finalOverride;
            }
        }
    }
}
