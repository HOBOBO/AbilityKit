using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Event;
using StableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class DamagePipelineService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageService _damage;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

        public DamagePipelineService(MobaActorLookupService actors, MobaDamageService damage, AbilityKit.Triggering.Eventing.IEventBus eventBus)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _eventBus = eventBus;
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

            ApplyFormula(calc);

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

                OriginSource = attack.OriginSource,
                OriginTarget = attack.OriginTarget,
                OriginKind = attack.OriginKind,
                OriginConfigId = attack.OriginConfigId,
                OriginContextId = attack.OriginContextId,

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

        private static void ApplyFormula(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var attack = calc.Attack;
            var kind = (DamageFormulaKind)attack.FormulaKind;
            if (kind == DamageFormulaKind.None) kind = DamageFormulaKind.Standard;

            switch (kind)
            {
                case DamageFormulaKind.Standard:
                default:
                {
                    // Step: base
                    var baseValue = attack.BaseDamage.Value;
                    var scaled = baseValue * attack.DamageRate.Value + attack.FlatBonus.Value;
                    calc.RawDamage.BaseValue = scaled;

                    // Step: mitigate (placeholder: no mitigation yet)
                    calc.MitigatedDamage.BaseValue = calc.RawDamage.Value;

                    // Step: shield (placeholder: none)
                    calc.ShieldAbsorb.BaseValue = 0f;
                    var hpDamage = System.Math.Max(0f, calc.MitigatedDamage.Value - calc.ShieldAbsorb.Value);
                    calc.HpDamage.BaseValue = hpDamage;

                    // Final override if any
                    var finalOverride = attack.FinalDamage.Value;
                    if (finalOverride > 0f)
                    {
                        calc.HpDamage.BaseValue = finalOverride;
                    }
                    break;
                }
            }

            PublishStatic(DamagePipelineEvents.AfterBase, calc);
            PublishStatic(DamagePipelineEvents.AfterMitigate, calc);
            PublishStatic(DamagePipelineEvents.AfterShield, calc);
            PublishStatic(DamagePipelineEvents.CalcFinal, calc);

            static void PublishStatic(string _, object __)
            {
                // placeholder: keeps old stage ordering calls centralized in Publish() below.
            }
        }

        private void Publish(string eventId, object payload)
        {
            var eventBus = _eventBus;
            if (eventBus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var eid = TriggeringIdUtil.GetEventEid(eventId);

            if (payload is AttackInfo ai2)
            {
                eventBus.Publish(new EventKey<AttackInfo>(eid), in ai2);
                object boxed = ai2;
                eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
            else if (payload is AttackCalcInfo ac2)
            {
                eventBus.Publish(new EventKey<AttackCalcInfo>(eid), in ac2);
                object boxed = ac2;
                eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
            else if (payload is DamageResult dr2)
            {
                eventBus.Publish(new EventKey<DamageResult>(eid), in dr2);
                object boxed = dr2;
                eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
            else
            {
                object boxed = payload;
                eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
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
