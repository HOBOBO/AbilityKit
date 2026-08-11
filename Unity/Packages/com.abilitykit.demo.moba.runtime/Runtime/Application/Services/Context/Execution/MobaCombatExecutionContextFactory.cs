using System;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaCombatExecutionContextFactory
    {
        public static MobaCombatExecutionContext Create(
            object payload,
            in MobaEffectLineageInput lineageInput,
            in MobaTriggerExecutionSnapshot executionSnapshot,
            int frame)
        {
            if (payload.TryResolveCombatExecutionContext(out var existing))
            {
                return WithSnapshot(in existing, in executionSnapshot, frame);
            }

            var origin = default(MobaGameplayOrigin);
            payload.TryResolveOrigin(out origin);

            if (!lineageInput.HasExecutionSource
                && !lineageInput.CanCreateRootExecution
                && !origin.HasExecutionSource
                && !executionSnapshot.HasExecutionSource)
            {
                var payloadType = payload != null ? payload.GetType().FullName : "null";
                throw new InvalidOperationException($"[MobaCombatExecutionContextFactory] Missing combat execution source. payloadType={payloadType}, lineageSourceActorId={lineageInput.SourceActorId}, lineageParentContextId={lineageInput.ParentContextId}, originSourceActorId={origin.SourceActorId}, originParentContextId={origin.EffectiveParentContextId}, snapshotSourceActorId={executionSnapshot.SourceActorId}, snapshotSourceContextId={executionSnapshot.SourceContextId}. Context creation requires sourceActorId and sourceContextId for execution.");
            }

            var builder = MobaTriggerExecutionSnapshotBuilder.Create()
                .FromLineage(in lineageInput)
                .FromPayload(payload)
                .FromSnapshot(in executionSnapshot);

            if (frame != 0)
            {
                builder.WithFrame(frame);
            }

            var snapshot = builder.Build();
            var handle = origin.SkillRuntimeHandle;
            if (!handle.IsValid && snapshot.SkillRuntimeHandle.IsValid)
            {
                handle = snapshot.SkillRuntimeHandle;
            }

            return new MobaCombatExecutionContext(payload, lineageInput, origin, snapshot, handle, frame);
        }

        public static MobaCombatExecutionContext WithSnapshot(
            in MobaCombatExecutionContext executionContext,
            in MobaTriggerExecutionSnapshot executionSnapshot,
            int frame)
        {
            var currentSnapshot = executionContext.ExecutionSnapshot;
            var snapshot = MobaTriggerExecutionSnapshotBuilder.Create()
                .FromSnapshot(in currentSnapshot)
                .FromSnapshot(in executionSnapshot)
                .Build();

            var handle = executionContext.SkillRuntimeHandle.IsValid
                ? executionContext.SkillRuntimeHandle
                : snapshot.SkillRuntimeHandle;

            return new MobaCombatExecutionContext(
                executionContext.Payload,
                executionContext.LineageInput,
                executionContext.Origin,
                snapshot,
                handle,
                frame != 0 ? frame : executionContext.Frame);
        }
    }
}
