using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class DamagePipelineService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageService _damage;
        private readonly IEventBus _events;

        public DamagePipelineService(MobaActorLookupService actors, MobaDamageService damage, IEventBus events)
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
            var kind = attack.FormulaKind;
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
            var bus = _events;
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            try
            {
                if (payload is AttackInfo ai)
                {
                    FillArgs(args, ai);
                }
                else if (payload is AttackCalcInfo ac && ac.Attack != null)
                {
                    FillArgs(args, ac.Attack);
                }
                else if (payload is DamageResult dr)
                {
                    args[EffectTriggering.Args.Source] = dr.AttackerActorId;
                    args[EffectTriggering.Args.Target] = dr.TargetActorId;
                    args[EffectTriggering.Args.OriginSource] = dr.OriginSource ?? dr.AttackerActorId;
                    args[EffectTriggering.Args.OriginTarget] = dr.OriginTarget ?? dr.TargetActorId;

                    if (dr.OriginKind != EffectSourceKind.None) args[EffectTriggering.Args.OriginKind] = dr.OriginKind;
                    if (dr.OriginConfigId != 0) args[EffectTriggering.Args.OriginConfigId] = dr.OriginConfigId;
                    if (dr.OriginContextId != 0) args[EffectTriggering.Args.OriginContextId] = dr.OriginContextId;
                }

                bus.Publish(new TriggerEvent(eventId, payload: payload, args: args));
            }
            catch
            {
                args.Dispose();
                throw;
            }
        }

        private static void FillArgs(PooledTriggerArgs args, AttackInfo attack)
        {
            if (args == null || attack == null) return;
            args[EffectTriggering.Args.Source] = attack.AttackerActorId;
            args[EffectTriggering.Args.Target] = attack.TargetActorId;
            args[EffectTriggering.Args.OriginSource] = attack.OriginSource ?? attack.AttackerActorId;
            args[EffectTriggering.Args.OriginTarget] = attack.OriginTarget ?? attack.TargetActorId;

            if (attack.OriginKind != EffectSourceKind.None) args[EffectTriggering.Args.OriginKind] = attack.OriginKind;
            if (attack.OriginConfigId != 0) args[EffectTriggering.Args.OriginConfigId] = attack.OriginConfigId;
            if (attack.OriginContextId != 0) args[EffectTriggering.Args.OriginContextId] = attack.OriginContextId;
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
