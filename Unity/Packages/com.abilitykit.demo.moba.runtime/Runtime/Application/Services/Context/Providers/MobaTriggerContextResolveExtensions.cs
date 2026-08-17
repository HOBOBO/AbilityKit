using System;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaTriggerContextResolveExtensions
    {
        public static bool TryResolveContextSource(this object payload, out MobaContextSourceView source)
        {
            source = default;
            if (payload == null) return false;

            var selectedKind = MobaContextSourceResolveKind.Unknown;
            if (payload is MobaContextSourceView direct)
            {
                ConsiderCandidate(payload, in direct, ref source, ref selectedKind);
            }

            if (payload is MobaPersistentContextSourceSnapshot directSnapshot
                && directSnapshot.TryGetContextSource(out var directSnapshotSource))
            {
                ConsiderCandidate(payload, in directSnapshotSource, ref source, ref selectedKind);
            }

            if (payload is MobaCombatExecutionContext directExecution)
            {
                var candidate = FromCombatExecution(in directExecution);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            if (payload is IMobaCombatContextSource combatSourceProvider
                && combatSourceProvider.TryGetCombatContextSource(out var combatSource)
                && combatSource.IsValid)
            {
                var candidate = combatSource.ToContextSourceView(
                    MobaContextSourceResolveKind.CombatExecutionContext,
                    MobaContextSourceBoundary.Execution);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            if (payload is IMobaCombatExecutionContextProvider executionContextProvider
                && executionContextProvider.TryGetCombatExecutionContext(out var executionContext))
            {
                var candidate = FromCombatExecution(in executionContext);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            if (payload is IMobaPersistentContextSourceProvider persistentProvider
                && persistentProvider.TryGetPersistentContextSource(out var snapshot)
                && snapshot.TryGetContextSource(out var persistentSource))
            {
                ConsiderCandidate(payload, in persistentSource, ref source, ref selectedKind);
            }

            if (payload is IMobaContextSourceProvider sourceProvider
                && sourceProvider.TryGetContextSource(out var providerSource))
            {
                ConsiderCandidate(payload, in providerSource, ref source, ref selectedKind);
            }

            if (payload is IMobaOriginContextProvider originProvider
                && originProvider.TryGetOrigin(out var origin)
                && origin.IsValid)
            {
                var candidate = MobaContextSourceView.FromOrigin(in origin);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            var skillRuntimeHandle = default(MobaSkillCastRuntimeHandle);
            if (payload is IMobaTriggerSkillRuntimeContext skillRuntimeProvider)
            {
                skillRuntimeProvider.TryGetSkillRuntimeHandle(out skillRuntimeHandle);
            }

            if (payload is IMobaTriggerLineageContextProvider lineageProvider
                && lineageProvider.TryGetLineageContext(out var lineageContext))
            {
                var candidate = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    skillRuntimeHandle: skillRuntimeHandle);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            if (payload is IMobaTriggerTraceContextProvider traceProvider
                && traceProvider.TryGetTraceContext(out var traceContext))
            {
                var candidate = MobaContextSourceView.FromTrace(
                    in traceContext,
                    skillRuntimeHandle);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            if (payload is IMobaTriggerExecutionSnapshotProvider snapshotProvider
                && snapshotProvider.TryGetExecutionSnapshot(out var executionSnapshot)
                && executionSnapshot.IsValid)
            {
                var candidate = MobaContextSourceView.FromExecutionSnapshot(in executionSnapshot);
                ConsiderCandidate(payload, in candidate, ref source, ref selectedKind);
            }

            return source.IsValid;
        }

        private static MobaContextSourceView FromCombatExecution(
            in MobaCombatExecutionContext executionContext)
        {
            return new MobaContextSourceView(
                MobaContextSourceResolveKind.CombatExecutionContext,
                MobaContextSourceBoundary.Execution,
                executionContext.ContextKind,
                executionContext.OriginKind,
                executionContext.SourceActorId,
                executionContext.TargetActorId,
                executionContext.ParentContextId,
                executionContext.ParentContextId,
                executionContext.RootContextId,
                executionContext.OwnerContextId,
                executionContext.ConfigId,
                executionContext.TriggerId,
                executionContext.Frame,
                null,
                0,
                false,
                executionContext.SkillRuntimeHandle);
        }

        private static void ConsiderCandidate(
            object payload,
            in MobaContextSourceView candidate,
            ref MobaContextSourceView selected,
            ref MobaContextSourceResolveKind selectedKind)
        {
            if (!candidate.IsValid) return;
            if (!selected.IsValid)
            {
                selected = candidate;
                selectedKind = candidate.ResolveKind;
                return;
            }

            var currentIdentity = MobaCanonicalProvenance.FromContextSource(in selected);
            var incomingIdentity = MobaCanonicalProvenance.FromContextSource(in candidate);
            var payloadType = payload.GetType().FullName;
            var mergedIdentity = currentIdentity.Merge(
                in incomingIdentity,
                selectedKind.ToString(),
                candidate.ResolveKind.ToString());
            var identityProjection = mergedIdentity.ApplyTo(in selected);
            selected = MergeProjection(
                in identityProjection,
                in candidate,
                payloadType,
                selectedKind);
        }

        private static MobaContextSourceView MergeProjection(
            in MobaContextSourceView selected,
            in MobaContextSourceView candidate,
            string payloadType,
            MobaContextSourceResolveKind selectedKind)
        {
            var contextKind = MergeValue(
                selected.ContextKind,
                candidate.ContextKind,
                EffectContextKind.Unknown,
                "contextKind",
                payloadType,
                selectedKind,
                candidate.ResolveKind);
            var triggerId = MergeValue(
                selected.TriggerId,
                candidate.TriggerId,
                0,
                "triggerId",
                payloadType,
                selectedKind,
                candidate.ResolveKind);
            var frame = MergeValue(
                selected.Frame,
                candidate.Frame,
                0,
                "frame",
                payloadType,
                selectedKind,
                candidate.ResolveKind);

            return new MobaContextSourceView(
                selected.ResolveKind,
                selected.Boundary,
                contextKind,
                selected.TraceKind != MobaTraceKind.None
                    ? selected.TraceKind
                    : candidate.TraceKind,
                selected.SourceActorId,
                selected.TargetActorId,
                selected.SourceContextId,
                selected.ParentContextId,
                selected.RootContextId,
                selected.OwnerContextId,
                selected.ConfigId != 0 ? selected.ConfigId : candidate.ConfigId,
                triggerId,
                frame,
                selected.RuntimeKind ?? candidate.RuntimeKind,
                selected.RuntimeConfigId != 0
                    ? selected.RuntimeConfigId
                    : candidate.RuntimeConfigId,
                selected.HasLiveRuntime || candidate.HasLiveRuntime,
                selected.SkillRuntimeHandle);
        }

        private static T MergeValue<T>(
            T current,
            T incoming,
            T missing,
            string fieldName,
            string payloadType,
            MobaContextSourceResolveKind selectedKind,
            MobaContextSourceResolveKind candidateKind)
            where T : struct
        {
            var currentMissing = current.Equals(missing);
            var incomingMissing = incoming.Equals(missing);
            if (currentMissing) return incoming;
            if (incomingMissing || current.Equals(incoming)) return current;

            throw new InvalidOperationException(
                $"[MobaTriggerContextResolveExtensions] Conflicting formal context providers. " +
                $"payloadType={payloadType}, field={fieldName}, current={current}, incoming={incoming}, selected={selectedKind}, candidate={candidateKind}.");
        }

        public static bool TryResolveExecutionSnapshot(this object payload, out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = default;
            return payload is IMobaTriggerExecutionSnapshotProvider provider
                   && provider.TryGetExecutionSnapshot(out snapshot)
                   && snapshot.IsValid;
        }

        public static bool TryResolveStageSnapshot(this object payload, out MobaTriggerStageSnapshot snapshot)
        {
            return MobaTriggerStageSnapshotResolver.TryResolve(payload, out snapshot);
        }

        public static bool TryResolveLineageContext(this object payload, out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = default;
            if (payload is IMobaTriggerLineageContextProvider lineageProvider && lineageProvider.TryGetLineageContext(out lineageContext))
                return true;

            if (payload is IMobaTriggerTraceContextProvider traceProvider && traceProvider.TryGetTraceContext(out var traceContext))
            {
                lineageContext = traceContext.ToLineageContext();
                return true;
            }

            return false;
        }

        public static bool TryResolveOrigin(this object payload, out MobaGameplayOrigin origin)
        {
            origin = default;
            if (payload is IMobaOriginContextProvider originProvider && originProvider.TryGetOrigin(out origin) && origin.IsValid)
                return true;

            if (payload.TryResolveLineageContext(out var lineageContext))
            {
                var handle = default(MobaSkillCastRuntimeHandle);
                if (payload is IMobaTriggerSkillRuntimeContext skillRuntimeProvider)
                {
                    skillRuntimeProvider.TryGetSkillRuntimeHandle(out handle);
                }

                origin = MobaGameplayOrigin.FromLineageContext(in lineageContext, in handle);
                return origin.IsValid;
            }

            return false;
        }
    }

    public enum MobaProvenanceFieldState
    {
        Missing = 0,
        Synthesized = 1,
        Inherited = 2,
        Explicit = 3
    }

    /// <summary>
    /// 上下文投影共享的规范身份。配置 ID 未纳入该模型，因为来源配置和执行配置
    /// 属于不同语义字段。
    /// </summary>
    public readonly struct MobaCanonicalProvenance
    {
        private MobaCanonicalProvenance(
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle,
            MobaProvenanceFieldState sourceActorState,
            MobaProvenanceFieldState targetActorState,
            MobaProvenanceFieldState sourceContextState,
            MobaProvenanceFieldState parentContextState,
            MobaProvenanceFieldState rootContextState,
            MobaProvenanceFieldState ownerContextState,
            MobaProvenanceFieldState skillRuntimeState)
        {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            SourceActorState = sourceActorState;
            TargetActorState = targetActorState;
            SourceContextState = sourceContextState;
            ParentContextState = parentContextState;
            RootContextState = rootContextState;
            OwnerContextState = ownerContextState;
            SkillRuntimeState = skillRuntimeState;
        }

        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long ParentContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public MobaProvenanceFieldState SourceActorState { get; }
        public MobaProvenanceFieldState TargetActorState { get; }
        public MobaProvenanceFieldState SourceContextState { get; }
        public MobaProvenanceFieldState ParentContextState { get; }
        public MobaProvenanceFieldState RootContextState { get; }
        public MobaProvenanceFieldState OwnerContextState { get; }
        public MobaProvenanceFieldState SkillRuntimeState { get; }

        public static MobaCanonicalProvenance FromContextSource(in MobaContextSourceView source)
        {
            var valueState = source.ResolveKind == MobaContextSourceResolveKind.DirectProvider
                ? MobaProvenanceFieldState.Explicit
                : source.Boundary == MobaContextSourceBoundary.LiveRuntime
                  || source.ResolveKind == MobaContextSourceResolveKind.RuntimeDebug
                    ? MobaProvenanceFieldState.Synthesized
                    : MobaProvenanceFieldState.Inherited;
            var handle = source.SkillRuntimeHandle;
            return new MobaCanonicalProvenance(
                source.SourceActorId,
                source.TargetActorId,
                source.SourceContextId,
                source.ParentContextId,
                source.RootContextId,
                source.OwnerContextId,
                in handle,
                StateFor(source.SourceActorId != 0, valueState),
                StateFor(source.TargetActorId != 0, valueState),
                StateFor(source.SourceContextId != 0L, valueState),
                StateFor(source.ParentContextId != 0L, valueState),
                StateFor(source.RootContextId != 0L, valueState),
                StateFor(source.OwnerContextId != 0L, valueState),
                StateFor(handle.IsValid, valueState));
        }

        public MobaCanonicalProvenance Merge(
            in MobaCanonicalProvenance incoming,
            string currentSource,
            string incomingSource)
        {
            var sourceActorId = Merge(SourceActorId, incoming.SourceActorId, "sourceActorId", currentSource, incomingSource);
            var targetActorId = Merge(TargetActorId, incoming.TargetActorId, "targetActorId", currentSource, incomingSource);
            var sourceContextId = Merge(SourceContextId, incoming.SourceContextId, "sourceContextId", currentSource, incomingSource);
            var parentContextId = Merge(ParentContextId, incoming.ParentContextId, "parentContextId", currentSource, incomingSource);
            var rootContextId = Merge(RootContextId, incoming.RootContextId, "rootContextId", currentSource, incomingSource);
            var ownerContextId = Merge(OwnerContextId, incoming.OwnerContextId, "ownerContextId", currentSource, incomingSource);
            var handle = MergeSkillRuntime(in incoming, currentSource, incomingSource);
            return new MobaCanonicalProvenance(
                sourceActorId,
                targetActorId,
                sourceContextId,
                parentContextId,
                rootContextId,
                ownerContextId,
                in handle,
                MergeState(SourceActorId != 0, SourceActorState, incoming.SourceActorState),
                MergeState(TargetActorId != 0, TargetActorState, incoming.TargetActorState),
                MergeState(SourceContextId != 0L, SourceContextState, incoming.SourceContextState),
                MergeState(ParentContextId != 0L, ParentContextState, incoming.ParentContextState),
                MergeState(RootContextId != 0L, RootContextState, incoming.RootContextState),
                MergeState(OwnerContextId != 0L, OwnerContextState, incoming.OwnerContextState),
                MergeState(SkillRuntimeHandle.IsValid, SkillRuntimeState, incoming.SkillRuntimeState));
        }

        private static MobaProvenanceFieldState StateFor(
            bool hasValue,
            MobaProvenanceFieldState valueState)
        {
            return hasValue ? valueState : MobaProvenanceFieldState.Missing;
        }

        private static MobaProvenanceFieldState MergeState(
            bool currentHasValue,
            MobaProvenanceFieldState current,
            MobaProvenanceFieldState incoming)
        {
            if (!currentHasValue) return incoming;
            return current >= incoming ? current : incoming;
        }

        public MobaContextSourceView ApplyTo(in MobaContextSourceView source)
        {
            return new MobaContextSourceView(
                source.ResolveKind,
                source.Boundary,
                source.ContextKind,
                source.TraceKind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                source.ConfigId,
                source.TriggerId,
                source.Frame,
                source.RuntimeKind,
                source.RuntimeConfigId,
                source.HasLiveRuntime,
                SkillRuntimeHandle);
        }

        private MobaSkillCastRuntimeHandle MergeSkillRuntime(
            in MobaCanonicalProvenance incoming,
            string currentSource,
            string incomingSource)
        {
            if (SkillRuntimeHandle.IsValid
                && incoming.SkillRuntimeHandle.IsValid
                && !SkillRuntimeHandle.Equals(incoming.SkillRuntimeHandle))
            {
                throw Conflict(
                    "skillRuntimeHandle",
                    SkillRuntimeHandle,
                    incoming.SkillRuntimeHandle,
                    currentSource,
                    incomingSource);
            }

            return SkillRuntimeHandle.IsValid
                ? SkillRuntimeHandle
                : incoming.SkillRuntimeHandle;
        }

        private static int Merge(
            int current,
            int incoming,
            string field,
            string currentSource,
            string incomingSource)
        {
            if (current != 0 && incoming != 0 && current != incoming)
                throw Conflict(field, current, incoming, currentSource, incomingSource);
            return current != 0 ? current : incoming;
        }

        private static long Merge(
            long current,
            long incoming,
            string field,
            string currentSource,
            string incomingSource)
        {
            if (current != 0L && incoming != 0L && current != incoming)
                throw Conflict(field, current, incoming, currentSource, incomingSource);
            return current != 0L ? current : incoming;
        }

        private static InvalidOperationException Conflict(
            string field,
            object current,
            object incoming,
            string currentSource,
            string incomingSource)
        {
            return new InvalidOperationException(
                $"[MobaCanonicalProvenance] Conflicting execution provenance. field={field}, current={current}, incoming={incoming}, currentSource={currentSource}, incomingSource={incomingSource}.");
        }
    }
}
