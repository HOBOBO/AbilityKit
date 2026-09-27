using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba
{
    public sealed class AttackInfo : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider,
        Services.IMobaContextSourceProvider, Services.IMobaCombatExecutionContextProvider,
        Services.IMobaCombatExecutionFactsProvider, Services.IMobaCombatContextSource
    {
        private Services.MobaCombatExecutionContext _executionContext;

        public int AttackerActorId
        {
            get => SourceActorId;
            set => SourceActorId = value;
        }

        public object OriginSource;
        public object OriginTarget;

        public MobaExecutionKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;
        public new Services.MobaGameplayOrigin Origin;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public int FormulaKind;
        public string FormulaId;
        public Services.MobaCombatExecutionFlags CombatFlags;

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
                OriginKind != Services.MobaExecutionKind.None ? OriginKind : Services.MobaExecutionKind.DamageAttack,
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

            lineageContext = new Services.MobaTriggerLineageContext(Services.EffectContextKind.Trigger, Services.MobaExecutionKind.DamageAttack, AttackerActorId, TargetActorId, OriginContextId, OriginContextId, 0, OriginConfigId);
            return AttackerActorId > 0 || TargetActorId > 0 || OriginContextId != 0;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetCombatExecutionContext(out var executionContext)
                && executionContext.TryGetContextSource(out source))
            {
                return true;
            }

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

        public bool TryGetCombatExecutionContext(out Services.MobaCombatExecutionContext context)
        {
            context = _executionContext;
            if (context.HasExecutionSource) return true;
            return Services.MobaCombatContextBuilder.TryFromSource(this, out context);
        }

        public bool TryGetCombatContextSource(out Services.MobaCombatContextSource source)
        {
            if (!TryGetOrigin(out var origin) || !origin.HasExecutionSource)
            {
                source = default;
                return false;
            }

            source = new Services.MobaCombatContextSource(
                Services.EffectContextKind.Trigger,
                origin.ImmediateKind != Services.MobaExecutionKind.None
                    ? origin.ImmediateKind
                    : Services.MobaExecutionKind.DamageAttack,
                AttackerActorId,
                TargetActorId,
                origin.EffectiveParentContextId,
                origin.EffectiveRootContextId,
                origin.OwnerContextId,
                origin.ImmediateConfigId,
                triggerId: TriggerId,
                skillRuntimeHandle: origin.SkillRuntimeHandle,
                runtimeKind: MobaRuntimeKindNames.DamageAttack,
                runtimeConfigId: OriginConfigId);
            return source.HasExecutionSource;
        }

        public bool TryGetCombatExecutionFacts(out Services.MobaCombatExecutionFacts facts)
        {
            facts = _executionContext.IsValid
                ? _executionContext.CombatFacts
                : new Services.MobaCombatExecutionFacts(CombatFlags);
            return true;
        }

        internal void SetExecutionContext(in Services.MobaCombatExecutionContext context)
        {
            _executionContext = context;
            SourceActorId = context.SourceActorId;
            TargetActorId = context.TargetActorId;
            SourceContextId = context.ParentContextId;
            TriggerId = context.TriggerId;
            CombatFlags = context.CombatFacts.Flags;
            var origin = context.Origin;
            SetOrigin(in origin);
        }
    }

    public sealed class AttackCalcInfo : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider,
        Services.IMobaContextSourceProvider, Services.IMobaCombatExecutionContextProvider,
        Services.IMobaCombatExecutionFactsProvider
    {
        private Services.MobaCombatExecutionContext _executionContext;
        private readonly Services.MobaCombatExecutionFacts _combatFacts;

        public AttackInfo Attack;

        public readonly CombatNumberValue RawDamage;
        public readonly CombatNumberValue MitigatedDamage;
        public readonly CombatNumberValue ShieldAbsorb;
        public readonly CombatNumberValue HpDamage;
        internal Services.ShieldAbsorbPlan ShieldPlan;

        public AttackCalcInfo(AttackInfo attack)
        {
            Attack = attack;
            _combatFacts = Services.MobaCombatExecutionFacts.Resolve(attack);
            SourceActorId = attack?.AttackerActorId ?? 0;
            TargetActorId = attack?.TargetActorId ?? 0;
            SourceContextId = attack?.SourceContextId ?? 0L;
            TriggerId = attack?.TriggerId ?? 0;
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
            if (_executionContext.TryGetOrigin(out origin)) return true;
            if (Attack != null) return Attack.TryGetOrigin(out origin);
            origin = default;
            return false;
        }

        public override bool TryGetLineageContext(out Services.MobaTriggerLineageContext lineageContext)
        {
            if (_executionContext.TryGetLineageContext(out lineageContext)) return true;
            if (Attack != null && Attack.TryGetLineageContext(out lineageContext)) return true;
            lineageContext = default;
            return false;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetCombatExecutionContext(out var executionContext)
                && executionContext.TryGetContextSource(out source))
            {
                return true;
            }

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

        public bool TryGetCombatExecutionContext(out Services.MobaCombatExecutionContext context)
        {
            context = _executionContext;
            return context.HasExecutionSource;
        }

        public bool TryGetCombatExecutionFacts(out Services.MobaCombatExecutionFacts facts)
        {
            facts = _executionContext.IsValid ? _executionContext.CombatFacts : _combatFacts;
            return true;
        }

        internal void SetExecutionContext(in Services.MobaCombatExecutionContext context)
        {
            _executionContext = context;
            SourceActorId = context.SourceActorId;
            TargetActorId = context.TargetActorId;
            SourceContextId = context.ParentContextId;
            TriggerId = context.TriggerId;
        }
    }

    public sealed class DamageResult : Services.MobaTriggerInvocationContextBase, Services.IMobaActorContextProvider,
        Services.IMobaContextSourceProvider, Services.IMobaCombatExecutionContextProvider,
        Services.IMobaCombatExecutionFactsProvider
    {
        private Services.MobaCombatExecutionContext _executionContext;

        public int AttackerActorId
        {
            get => SourceActorId;
            set => SourceActorId = value;
        }

        public object OriginSource;
        public object OriginTarget;

        public MobaExecutionKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;
        public new Services.MobaGameplayOrigin Origin;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;
        public Services.MobaCombatExecutionFlags CombatFlags;

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
                OriginKind != Services.MobaExecutionKind.None ? OriginKind : Services.MobaExecutionKind.DamageApply,
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
                var damageOrigin = origin.WithImmediate(Services.MobaExecutionKind.DamageApply, ReasonParam, origin.EffectiveParentContextId);
                lineageContext = damageOrigin.ToLineageContext(Services.EffectContextKind.Trigger);
                return true;
            }

            lineageContext = new Services.MobaTriggerLineageContext(Services.EffectContextKind.Trigger, Services.MobaExecutionKind.DamageApply, AttackerActorId, TargetActorId, OriginContextId, OriginContextId, 0, ReasonParam);
            return AttackerActorId > 0 || TargetActorId > 0 || OriginContextId != 0;
        }

        public bool TryGetContextSource(out Services.MobaContextSourceView source)
        {
            if (TryGetCombatExecutionContext(out var executionContext)
                && executionContext.TryGetContextSource(out source))
            {
                return true;
            }

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

        public bool TryGetCombatExecutionContext(out Services.MobaCombatExecutionContext context)
        {
            context = _executionContext;
            return context.HasExecutionSource;
        }

        public bool TryGetCombatExecutionFacts(out Services.MobaCombatExecutionFacts facts)
        {
            facts = _executionContext.IsValid
                ? _executionContext.CombatFacts
                : new Services.MobaCombatExecutionFacts(CombatFlags);
            return true;
        }

        internal void SetExecutionContext(in Services.MobaCombatExecutionContext context)
        {
            _executionContext = context;
            SourceActorId = context.SourceActorId;
            TargetActorId = context.TargetActorId;
            SourceContextId = context.ParentContextId;
            TriggerId = context.TriggerId;
            CombatFlags = context.CombatFacts.Flags;
            var origin = context.Origin;
            SetOrigin(in origin);
        }
    }
}
