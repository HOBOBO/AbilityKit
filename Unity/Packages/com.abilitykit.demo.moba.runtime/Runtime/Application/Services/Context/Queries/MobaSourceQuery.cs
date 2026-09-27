namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaSourceQuery
    {
        public MobaSourceQuery(in MobaContextSourceView source)
            : this(in source, default(MobaGameplayOrigin))
        {
        }

        public MobaSourceQuery(in MobaContextSourceView source, in MobaGameplayOrigin origin)
        {
            Source = source;
            Origin = origin;
        }

        public MobaContextSourceView Source { get; }
        public MobaGameplayOrigin Origin { get; }
        public bool IsValid => Source.IsValid || Origin.IsValid;
        public EffectContextKind ContextKind => Source.ContextKind != EffectContextKind.Unknown ? Source.ContextKind : EffectContextKind.Unknown;
        public MobaExecutionKind ExecutionKind => Source.ExecutionKind != MobaExecutionKind.None ? Source.ExecutionKind : Origin.ImmediateKind;
        public int SourceActorId => Source.SourceActorId != 0 ? Source.SourceActorId : Origin.SourceActorId;
        public int TargetActorId => Source.TargetActorId != 0 ? Source.TargetActorId : Origin.TargetActorId;
        public long SourceContextId => Source.SourceContextId != 0 ? Source.SourceContextId : Origin.ImmediateContextId;
        public long ParentContextId => Source.ParentContextId != 0 ? Source.ParentContextId : Origin.EffectiveParentContextId;
        public long RootContextId => Source.RootContextId != 0 ? Source.RootContextId : Origin.EffectiveRootContextId;
        public long OwnerContextId => Source.OwnerContextId != 0 ? Source.OwnerContextId : Origin.OwnerContextId;
        public int ConfigId => Source.ConfigId != 0 ? Source.ConfigId : Origin.ImmediateConfigId;
        public string RuntimeKind => Source.RuntimeKind;
        public int RuntimeConfigId => Source.RuntimeConfigId != 0 ? Source.RuntimeConfigId : ConfigId;
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle => Source.SkillRuntimeHandle.IsValid ? Source.SkillRuntimeHandle : Origin.SkillRuntimeHandle;
        public bool HasLiveRuntime => Source.HasLiveRuntime;
        public MobaContextSourceBoundary Boundary => Source.Boundary;

        public bool IsContextKind(EffectContextKind kind)
        {
            return kind != EffectContextKind.Unknown && ContextKind == kind;
        }

        public bool IsExecutionKind(MobaExecutionKind kind)
        {
            return kind != MobaExecutionKind.None && ExecutionKind == kind;
        }

        public bool IsRuntimeKind(string runtimeKind)
        {
            return !string.IsNullOrEmpty(runtimeKind) && string.Equals(RuntimeKind, runtimeKind, System.StringComparison.Ordinal);
        }

        public bool IsRuntime(string runtimeKind, int runtimeConfigId)
        {
            return IsRuntimeKind(runtimeKind) && (runtimeConfigId == 0 || RuntimeConfigId == runtimeConfigId || ConfigId == runtimeConfigId);
        }

        public bool IsBuff()
        {
            return IsContextKind(EffectContextKind.Buff) || IsRuntimeKind(MobaRuntimeKindNames.Buff) || IsExecutionKind(MobaExecutionKind.BuffApply) || IsExecutionKind(MobaExecutionKind.BuffTick) || IsExecutionKind(MobaExecutionKind.BuffRemove);
        }

        public bool IsBuff(int buffId)
        {
            return IsBuff() && (buffId == 0 || ConfigId == buffId || RuntimeConfigId == buffId);
        }

        public bool IsDamage()
        {
            return IsRuntimeKind(MobaRuntimeKindNames.DamageAttack) || IsRuntimeKind(MobaRuntimeKindNames.DamageCalc) || IsRuntimeKind(MobaRuntimeKindNames.DamageResult) || IsExecutionKind(MobaExecutionKind.DamageAttack) || IsExecutionKind(MobaExecutionKind.DamageCalc) || IsExecutionKind(MobaExecutionKind.DamageApply);
        }

        public bool HasContext(long contextId)
        {
            if (contextId == 0) return false;
            return SourceContextId == contextId || ParentContextId == contextId || RootContextId == contextId || OwnerContextId == contextId;
        }

        public bool SharesRootWith(in MobaSourceQuery other)
        {
            return RootContextId != 0 && RootContextId == other.RootContextId;
        }
    }

    public static class MobaSourceQueryResolver
    {
        public static bool TryResolve(object payload, out MobaSourceQuery query)
        {
            query = default(MobaSourceQuery);
            if (payload == null) return false;

            var hasSource = payload.TryResolveContextSource(out var source);
            MobaGameplayOrigin origin = default(MobaGameplayOrigin);
            var hasOrigin = payload is IMobaOriginContextProvider originProvider && originProvider.TryGetOrigin(out origin) && origin.IsValid;

            if (!hasSource && hasOrigin)
            {
                source = MobaContextSourceView.FromOrigin(in origin);
                hasSource = source.IsValid;
            }

            query = hasSource || hasOrigin ? new MobaSourceQuery(in source, in origin) : default(MobaSourceQuery);
            return query.IsValid;
        }

        public static MobaSourceQuery ResolveOrDefault(object payload)
        {
            return TryResolve(payload, out var query) ? query : default(MobaSourceQuery);
        }
    }
}
