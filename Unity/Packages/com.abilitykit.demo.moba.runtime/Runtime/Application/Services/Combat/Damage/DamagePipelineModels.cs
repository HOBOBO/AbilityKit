using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba
{
    public sealed class AttackInfo : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider, Services.IMobaContextSourceProvider
    {
        public int AttackerActorId;
        public int TargetActorId;

        public object OriginSource;
        public object OriginTarget;

        public MobaTraceKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;
        public Services.MobaGameplayOrigin Origin;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public int FormulaKind;
        public string FormulaId;

        public readonly CombatNumberValue BaseDamage;
        public readonly CombatNumberValue DamageRate;
        public readonly CombatNumberValue FlatBonus;
        public readonly CombatNumberValue FinalDamage;

        public AttackInfo()
        {
            BaseDamage = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
            DamageRate = new CombatNumberValue(CombatNumberValueMode.BaseAddMul, baseValue: AbilityKit.Deterministic.Fixed64.One);
            FlatBonus = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
            FinalDamage = new CombatNumberValue(CombatNumberValueMode.OverrideOnly);
        }

        public override Services.EffectContextKind Kind => Services.EffectContextKind.Trigger;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = AttackerActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public override bool TryGetOrigin(out Services.MobaGameplayOrigin origin)
        {
            if (Origin.IsValid)
            {
                origin = Origin;
                return true;
            }

            var sourceActorId = OriginSource is int source ? source : AttackerActorId;
            var targetActorId = OriginTarget is int target ? target : TargetActorId;
            var lineageContext = new Services.MobaTriggerLineageContext(
                Services.EffectContextKind.Trigger,
                OriginKind != Services.MobaTraceKind.None ? OriginKind : Services.MobaTraceKind.DamageAttack,
                sourceActorId,
                targetActorId,
                OriginContextId,
                OriginContextId,
                OriginContextId,
                OriginConfigId);
            origin = Services.MobaGameplayOrigin.FromLineageContext(in lineageContext);
            return origin.IsValid;
        }

        public override bool TryGetLineageContext(out Services.MobaTriggerLineageContext lineageContext)
        {
            if (TryGetOrigin(out var origin) && origin.IsValid)
            {
                lineageContext = origin.ToLineageContext(Services.EffectContextKind.Trigger);
                return true;
            }

            lineageContext = new Services.MobaTriggerLineageContext(Services.EffectContextKind.Trigger, Services.MobaTraceKind.DamageAttack, AttackerActorId, TargetActorId, OriginContextId, OriginContextId, 0, OriginConfigId);
            return AttackerActorId > 0 || TargetActorId > 0 || OriginContextId != 0;
        }

        public override bool TryGetTraceContext(out Services.MobaTriggerTraceContext traceContext)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                traceContext = lineageContext.ToTraceContext();
                return true;
            }

            traceContext = default;
            return false;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = Services.MobaContextSourceView.FromLineage(
                    in lineageContext,
                    Services.MobaContextSourceResolveKind.DirectProvider,
                    Services.MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.DamageAttack,
                    runtimeConfigId: OriginConfigId);
                return source.IsValid;
            }

            source = default;
            return false;
        }

        public void SetOrigin(in Services.MobaGameplayOrigin origin)
        {
            Origin = origin;
            OriginSource = origin.SourceActorId;
            OriginTarget = origin.TargetActorId;
            OriginKind = origin.ImmediateKind;
            OriginConfigId = origin.ImmediateConfigId;
            OriginContextId = origin.EffectiveParentContextId;
        }
    }

    public sealed class AttackCalcInfo : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider, Services.IMobaContextSourceProvider
    {
        public AttackInfo Attack;

        public readonly CombatNumberValue RawDamage;
        public readonly CombatNumberValue MitigatedDamage;
        public readonly CombatNumberValue ShieldAbsorb;
        public readonly CombatNumberValue HpDamage;
        internal Services.ShieldAbsorbPlan ShieldPlan;

        public AttackCalcInfo(AttackInfo attack)
        {
            Attack = attack;
            RawDamage = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
            MitigatedDamage = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
            ShieldAbsorb = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
            HpDamage = new CombatNumberValue(CombatNumberValueMode.BaseAddMul);
        }

        public override Services.EffectContextKind Kind => Services.EffectContextKind.Trigger;

        public bool TryGetSourceActorId(out int actorId)
        {
            if (Attack != null) return Attack.TryGetSourceActorId(out actorId);
            actorId = 0;
            return false;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            if (Attack != null) return Attack.TryGetTargetActorId(out actorId);
            actorId = 0;
            return false;
        }

        public override bool TryGetOrigin(out Services.MobaGameplayOrigin origin)
        {
            if (Attack != null) return Attack.TryGetOrigin(out origin);
            origin = default;
            return false;
        }

        public override bool TryGetLineageContext(out Services.MobaTriggerLineageContext lineageContext)
        {
            if (Attack != null && Attack.TryGetLineageContext(out lineageContext)) return true;
            lineageContext = default;
            return false;
        }

        public override bool TryGetTraceContext(out Services.MobaTriggerTraceContext traceContext)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                traceContext = lineageContext.ToTraceContext();
                return true;
            }

            traceContext = default;
            return false;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = Services.MobaContextSourceView.FromLineage(
                    in lineageContext,
                    Services.MobaContextSourceResolveKind.DirectProvider,
                    Services.MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.DamageCalc,
                    runtimeConfigId: lineageContext.SourceConfigId);
                return source.IsValid;
            }

            source = default;
            return false;
        }
    }

    public sealed class DamageResult : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider, Services.IMobaContextSourceProvider
    {
        public int AttackerActorId;
        public int TargetActorId;

        public object OriginSource;
        public object OriginTarget;

        public MobaTraceKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;
        public Services.MobaGameplayOrigin Origin;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public float Value;
        public float TargetHp;
        public float TargetMaxHp;

        public override Services.EffectContextKind Kind => Services.EffectContextKind.Trigger;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = AttackerActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public override bool TryGetOrigin(out Services.MobaGameplayOrigin origin)
        {
            if (Origin.IsValid)
            {
                origin = Origin;
                return true;
            }

            var sourceActorId = OriginSource is int source ? source : AttackerActorId;
            var targetActorId = OriginTarget is int target ? target : TargetActorId;
            var lineageContext = new Services.MobaTriggerLineageContext(
                Services.EffectContextKind.Trigger,
                OriginKind != Services.MobaTraceKind.None ? OriginKind : Services.MobaTraceKind.DamageApply,
                sourceActorId,
                targetActorId,
                OriginContextId,
                OriginContextId,
                OriginContextId,
                OriginConfigId != 0 ? OriginConfigId : ReasonParam);
            origin = Services.MobaGameplayOrigin.FromLineageContext(in lineageContext);
            return origin.IsValid;
        }

        public override bool TryGetLineageContext(out Services.MobaTriggerLineageContext lineageContext)
        {
            if (TryGetOrigin(out var origin) && origin.IsValid)
            {
                var damageOrigin = origin.WithImmediate(Services.MobaTraceKind.DamageApply, ReasonParam, origin.EffectiveParentContextId);
                lineageContext = damageOrigin.ToLineageContext(Services.EffectContextKind.Trigger);
                return true;
            }

            lineageContext = new Services.MobaTriggerLineageContext(Services.EffectContextKind.Trigger, Services.MobaTraceKind.DamageApply, AttackerActorId, TargetActorId, OriginContextId, OriginContextId, 0, ReasonParam);
            return AttackerActorId > 0 || TargetActorId > 0 || OriginContextId != 0;
        }

        public override bool TryGetTraceContext(out Services.MobaTriggerTraceContext traceContext)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                traceContext = lineageContext.ToTraceContext();
                return true;
            }

            traceContext = default;
            return false;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = Services.MobaContextSourceView.FromLineage(
                    in lineageContext,
                    Services.MobaContextSourceResolveKind.DirectProvider,
                    Services.MobaContextSourceBoundary.Snapshot,
                    runtimeKind: MobaRuntimeKindNames.DamageResult,
                    runtimeConfigId: ReasonParam);
                return source.IsValid;
            }

            source = default;
            return false;
        }

        public void SetOrigin(in Services.MobaGameplayOrigin origin)
        {
            Origin = origin;
            OriginSource = origin.SourceActorId;
            OriginTarget = origin.TargetActorId;
            OriginKind = origin.ImmediateKind;
            OriginConfigId = origin.ImmediateConfigId;
            OriginContextId = origin.EffectiveParentContextId;
        }
    }
}
