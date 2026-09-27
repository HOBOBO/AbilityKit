namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaActionOriginBuilder
    {
        public static MobaGameplayOrigin Build(
            in MobaCombatExecutionContext executionContext,
            in MobaEffectExecutionScopeSnapshot executionScope,
            int sourceActorId,
            int targetActorId,
            MobaExecutionKind fallbackKind,
            int fallbackConfigId)
        {
            var origin = ResolveOrigin(
                in executionContext,
                sourceActorId,
                targetActorId,
                fallbackKind,
                fallbackConfigId);

            return BuildFromOrigin(in origin, in executionContext, in executionScope, sourceActorId, targetActorId);
        }

        public static MobaGameplayOrigin BuildFromOrigin(
            in MobaGameplayOrigin sourceOrigin,
            in MobaCombatExecutionContext executionContext,
            in MobaEffectExecutionScopeSnapshot executionScope,
            int sourceActorId,
            int targetActorId)
        {
            var origin = sourceOrigin.WithActors(sourceActorId, targetActorId);
            if (executionScope.EffectContextId != 0)
            {
                var handle = executionContext.SkillRuntimeHandle;
                var parentContextId = executionScope.CurrentActionContextId != 0
                    ? executionScope.CurrentActionContextId
                    : executionScope.EffectContextId;
                var immediateKind = executionScope.CurrentActionContextId != 0
                    ? MobaExecutionKind.EffectAction
                    : MobaExecutionKind.EffectExecution;
                var immediateConfigId = executionScope.CurrentActionContextId != 0
                    ? (int)executionScope.CurrentActionId
                    : executionScope.EffectConfigId;
                var rootContextId = origin.EffectiveRootContextId != 0
                    ? origin.EffectiveRootContextId
                    : executionScope.EffectContextId;
                var ownerContextId = origin.OwnerContextId != 0
                    ? origin.OwnerContextId
                    : parentContextId;
                return MobaGameplayOriginBuilder.Create()
                    .FromOrigin(in origin)
                    .WithActors(sourceActorId, targetActorId)
                    .WithImmediate(immediateKind, immediateConfigId, parentContextId)
                    .WithRootContext(rootContextId)
                    .WithOwnerContext(ownerContextId)
                    .WithSkillRuntimeIfMissing(in handle)
                    .Build();
            }

            if (!origin.SkillRuntimeHandle.IsValid && executionContext.SkillRuntimeHandle.IsValid)
            {
                var handle = executionContext.SkillRuntimeHandle;
                origin = origin.WithSkillRuntime(in handle);
            }

            return origin;
        }

        private static MobaGameplayOrigin ResolveOrigin(
            in MobaCombatExecutionContext executionContext,
            int sourceActorId,
            int targetActorId,
            MobaExecutionKind fallbackKind,
            int fallbackConfigId)
        {
            if (executionContext.TryGetOrigin(out var contextOrigin) && contextOrigin.IsValid)
            {
                return contextOrigin;
            }

            if (executionContext.TryGetLineageContext(out var lineageContext))
            {
                return MobaGameplayOrigin.FromLineageContext(in lineageContext, executionContext.SkillRuntimeHandle)
                    .WithActors(sourceActorId, targetActorId);
            }

            return MobaGameplayOriginBuilder.Create()
                .WithActors(sourceActorId, targetActorId)
                .WithImmediate(fallbackKind, fallbackConfigId, 0)
                .WithSkillRuntimeIfMissing(executionContext.SkillRuntimeHandle)
                .Build();
        }
    }
}
