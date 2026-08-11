using System;
using AbilityKit.Effect;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaEffectLineageInputResolver
    {
        public static MobaEffectLineageInput Resolve(object payload)
        {
            if (payload.TryResolveContextSource(out var source)
                && source.HasExecutionSource)
            {
                return new MobaEffectLineageInput(
                    source.ContextKind,
                    source.TraceKind,
                    source.SourceActorId,
                    source.TargetActorId,
                    source.SourceContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    source.ConfigId);
            }

            if (payload is IMobaTriggerInvocationContext invocation)
            {
                var lineageInput = MobaEffectLineageInput.FromInvocation(invocation);
                if (lineageInput.HasExecutionSource)
                {
                    return lineageInput;
                }
            }

            if (payload is IEffectContext effectCtx)
            {
                var lineageInput = new MobaEffectLineageInput(
                    effectCtx.Kind,
                    MobaTraceKind.EffectExecution,
                    effectCtx.SourceActorId,
                    effectCtx.TargetActorId,
                    effectCtx.SourceContextId,
                    effectCtx.SourceContextId,
                    0,
                    0);
                if (lineageInput.HasExecutionSource)
                {
                    return lineageInput;
                }
            }

            // Actor IDs and trace context IDs use different namespaces. An actor-only payload
            // may start a new effect root, but it must never be promoted to a fake trace parent.
            if (payload is IMobaActorContextProvider actorProvider
                && actorProvider.TryGetSourceActorId(out var fallbackSource)
                && fallbackSource > 0)
            {
                actorProvider.TryGetTargetActorId(out var fallbackTarget);
                var fallbackKind = payload is IMobaTriggerInvocationContext fallbackInvocation
                    ? fallbackInvocation.Kind
                    : EffectContextKind.Trigger;
                return new MobaEffectLineageInput(
                    fallbackKind,
                    MobaTraceKind.EffectExecution,
                    fallbackSource,
                    fallbackTarget,
                    0L,
                    0L,
                    0L,
                    0);
            }

            var payloadType = payload != null ? payload.GetType().FullName : "null";
            throw new InvalidOperationException($"[MobaEffectLineageInputResolver] Missing complete effect lineage context. payloadType={payloadType}. Effect execution payload must provide sourceActorId and sourceContextId through IMobaCombatContextSource, IMobaCombatExecutionContextProvider, IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaTriggerInvocationContext, IEffectContext, or IMobaActorContextProvider.");
        }
    }
}
