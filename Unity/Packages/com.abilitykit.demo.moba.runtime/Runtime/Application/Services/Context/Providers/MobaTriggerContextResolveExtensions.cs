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

            if (!HasFormalIdentityConflict(in selected, in candidate)) return;

            var payloadType = payload.GetType().FullName;
            throw new InvalidOperationException(
                $"[MobaTriggerContextResolveExtensions] Conflicting formal context providers. " +
                $"payloadType={payloadType} selected={selectedKind} candidate={candidate.ResolveKind} " +
                $"selectedSourceActor={selected.SourceActorId} candidateSourceActor={candidate.SourceActorId} " +
                $"selectedSourceContext={selected.SourceContextId} candidateSourceContext={candidate.SourceContextId} " +
                $"selectedRootContext={selected.RootContextId} candidateRootContext={candidate.RootContextId} " +
                $"selectedOwnerContext={selected.OwnerContextId} candidateOwnerContext={candidate.OwnerContextId}.");
        }

        private static bool HasFormalIdentityConflict(
            in MobaContextSourceView left,
            in MobaContextSourceView right)
        {
            return Conflicts(left.SourceActorId, right.SourceActorId)
                   || Conflicts(left.SourceContextId, right.SourceContextId)
                   || Conflicts(left.ParentContextId, right.ParentContextId)
                   || Conflicts(left.RootContextId, right.RootContextId)
                   || Conflicts(left.OwnerContextId, right.OwnerContextId);
        }

        private static bool Conflicts(long left, long right)
        {
            return left != 0L && right != 0L && left != right;
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
}
