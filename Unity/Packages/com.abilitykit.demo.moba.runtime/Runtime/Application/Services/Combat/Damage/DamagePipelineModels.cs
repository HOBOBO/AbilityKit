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

        public readonly DamageNumberValue BaseDamage;
        public readonly DamageNumberValue DamageRate;
        public readonly DamageNumberValue FlatBonus;
        public readonly DamageNumberValue FinalDamage;

        public AttackInfo()
        {
            BaseDamage = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
            DamageRate = new DamageNumberValue(DamageNumberValueMode.BaseAddMul, baseValue: 1f);
            FlatBonus = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
            FinalDamage = new DamageNumberValue(DamageNumberValueMode.OverrideOnly);
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

        public readonly DamageNumberValue RawDamage;
        public readonly DamageNumberValue MitigatedDamage;
        public readonly DamageNumberValue ShieldAbsorb;
        public readonly DamageNumberValue HpDamage;
        internal Services.ShieldAbsorbPlan ShieldPlan;

        public AttackCalcInfo(AttackInfo attack)
        {
            Attack = attack;
            RawDamage = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
            MitigatedDamage = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
            ShieldAbsorb = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
            HpDamage = new DamageNumberValue(DamageNumberValueMode.BaseAddMul);
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
