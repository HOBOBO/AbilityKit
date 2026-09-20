using System;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Events.Buff;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Demo.Moba.Systems
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class MobaTriggerEventAttribute : Attribute
    {
        public string EventIdOrPrefix { get; }
        public Type ArgsType { get; }
        public bool IsPrefix { get; }

        public MobaTriggerEventAttribute(string eventIdOrPrefix, Type argsType, bool isPrefix = false)
        {
            EventIdOrPrefix = eventIdOrPrefix;
            ArgsType = argsType;
            IsPrefix = isPrefix;
        }
    }

    [MobaTriggerEvent("skill.", typeof(SkillCastContext), isPrefix: true)]
    [MobaTriggerEvent(MobaBuffTriggering.Prefixes.Buff, typeof(BuffEventArgs), isPrefix: true)]
    [MobaTriggerEvent(TriggeringConstants.Events.OnBuffAdded, typeof(BuffEventArgs))]
    [MobaTriggerEvent("area.", typeof(AreaEventArgs), isPrefix: true)]
    [MobaTriggerEvent(EffectTriggering.EventNames.Apply, typeof(EffectEventArgs))]
    [MobaTriggerEvent(EffectTriggering.EventNames.Tick, typeof(EffectEventArgs))]
    [MobaTriggerEvent(EffectTriggering.EventNames.Remove, typeof(EffectEventArgs))]
    [MobaTriggerEvent("presentation.", typeof(AbilityKit.Demo.Moba.Triggering.PresentationEventArgs), isPrefix: true)]
    [MobaTriggerEvent("gameplay.", typeof(GameplayLifecycleEventArgs), isPrefix: true)]
    [MobaTriggerEvent(DamagePipelineEvents.AttackCreated, typeof(AttackInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.BeforeCalc, typeof(AttackInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.CalcBegin, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.AfterBase, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.AfterMitigate, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.AfterShield, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.CalcFinal, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.BeforeApply, typeof(AttackCalcInfo))]
    [MobaTriggerEvent(DamagePipelineEvents.AfterApply, typeof(DamageResult))]
    [MobaTriggerEvent(DamagePipelineEvents.HealthCommitted, typeof(MobaHealthChangeResult))]
    [MobaTriggerEvent(HealPipelineEvents.BeforeApply, typeof(MobaHealRequest))]
    [MobaTriggerEvent(HealPipelineEvents.AfterApply, typeof(MobaHealthChangeResult))]
    [MobaTriggerEvent(ProjectileTriggering.Events.Hit, typeof(ProjectileHitEvent))]
    [MobaTriggerEvent(ProjectileTriggering.Events.Spawn, typeof(ProjectileSpawnEvent))]
    [MobaTriggerEvent(ProjectileTriggering.Events.Tick, typeof(ProjectileTickEvent))]
    [MobaTriggerEvent(ProjectileTriggering.Events.Exit, typeof(ProjectileExitEvent))]
    [MobaTriggerEvent("summon.", typeof(SummonEventPayload), isPrefix: true)]
    [MobaTriggerEvent("unit.", typeof(UnitEventPayload), isPrefix: true)]
    [MobaTriggerEvent(MobaUnitTriggering.Events.Die, typeof(UnitDieEventPayload))]
    internal static class MobaTriggerEventMappings
    {
    }
}
