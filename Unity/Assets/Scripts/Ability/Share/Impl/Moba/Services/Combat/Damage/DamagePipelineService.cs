using System;
using AbilityKit.Ability.Impl.Moba.Attributes;
using AbilityKit.Ability.Share.Common.Numbers;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class DamagePipelineService_Obsolete : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageService _damage;
        private readonly IEventBus _events;

        public DamagePipelineService_Obsolete(MobaActorLookupService actors, MobaDamageService damage, IEventBus events)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _events = events;
        }

        public DamageResult Execute(AttackInfo attack)
        {
            if (attack == null) return null;
            if (attack.TargetActorId <= 0) return null;

            if (!_actors.TryGetActorEntity(attack.TargetActorId, out var target) || target == null) return null;

            Publish(DamagePipelineEvents.AttackCreated, attack);
            Publish(DamagePipelineEvents.BeforeCalc, attack);

            var calc = new AttackCalcInfo(attack);

            Publish(DamagePipelineEvents.CalcBegin, calc);

            // Step: base
            var baseValue = attack.BaseDamage.Value;
            var scaled = baseValue * attack.DamageRate.Value + attack.FlatBonus.Value;
            calc.RawDamage.BaseValue = scaled;
            Publish(DamagePipelineEvents.AfterBase, calc);

            // Step: mitigate (placeholder: no mitigation yet)
            calc.MitigatedDamage.BaseValue = calc.RawDamage.Value;
            Publish(DamagePipelineEvents.AfterMitigate, calc);

            // Step: shield (placeholder: none)
            calc.ShieldAbsorb.BaseValue = 0f;
            var hpDamage = System.Math.Max(0f, calc.MitigatedDamage.Value - calc.ShieldAbsorb.Value);
            calc.HpDamage.BaseValue = hpDamage;
            Publish(DamagePipelineEvents.AfterShield, calc);

            // Final override if any
            var finalOverride = attack.FinalDamage.Value;
            if (finalOverride > 0f)
            {
                calc.HpDamage.BaseValue = finalOverride;
            }
            Publish(DamagePipelineEvents.CalcFinal, calc);

            Publish(DamagePipelineEvents.BeforeApply, calc);

            var targetAttrs = target.GetMobaAttrs();
            var oldHp = targetAttrs.Hp;
            var maxHp = targetAttrs.MaxHp;

            var applied = _damage.ApplyDamage(
                attackerActorId: attack.AttackerActorId,
                targetActorId: attack.TargetActorId,
                damageType: (int)attack.DamageType,
                value: calc.HpDamage.Value,
                reasonKind: (int)attack.ReasonKind,
                reasonParam: attack.ReasonParam);

            var result = new DamageResult
            {
                AttackerActorId = attack.AttackerActorId,
                TargetActorId = attack.TargetActorId,
                DamageType = attack.DamageType,
                CritType = attack.CritType,
                ReasonKind = attack.ReasonKind,
                ReasonParam = attack.ReasonParam,
                Value = applied,
                TargetHp = Clamp(oldHp - applied, 0f, maxHp),
                TargetMaxHp = maxHp,
            };

            Publish(DamagePipelineEvents.AfterApply, result);
            return result;
        }

        private void Publish(string eventId, object payload)
        {
            var bus = _events;
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;
            bus.Publish(new TriggerEvent(eventId, payload: payload, args: null));
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public void Dispose()
        {
        }
    }
}
