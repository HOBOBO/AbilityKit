using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal static class MobaPlanActionExecutionContextResolver
    {
        public static bool TryResolve(object triggerArgs, ExecCtx<IWorldResolver> ctx, out MobaCombatExecutionContext executionContext)
        {
            var services = ctx.Context;
            if (services != null
                && services.TryResolve<MobaEffectExecutionService>(out var effects)
                && effects != null
                && effects.TryGetCurrentExecutionContext(out var currentContext))
            {
                executionContext = currentContext;
                return true;
            }

            if (triggerArgs.TryResolveCombatExecutionContext(out var payloadContext))
            {
                executionContext = payloadContext;
                return true;
            }

            executionContext = default;
            return false;
        }

        public static MobaCombatExecutionContext Resolve(object triggerArgs, ExecCtx<IWorldResolver> ctx)
        {
            if (TryResolve(triggerArgs, ctx, out var executionContext))
            {
                return executionContext;
            }

            var payloadType = triggerArgs != null ? triggerArgs.GetType().FullName : "null";
            throw new System.InvalidOperationException($"[MobaPlanActionExecutionContextResolver] Missing combat execution context. payloadType={payloadType}. Action must run inside MobaEffectExecutionService session or provide IMobaCombatContextSource/IMobaCombatExecutionContextProvider.");
        }

        public static bool TryResolveExecutionScope(ExecCtx<IWorldResolver> ctx, out MobaEffectExecutionScopeSnapshot executionScope)
        {
            executionScope = default;
            var services = ctx.Context;
            return services != null
                   && services.TryResolve<MobaEffectExecutionService>(out var effects)
                   && effects != null
                   && effects.TryGetCurrentExecutionScope(out executionScope)
                   && executionScope.EffectContextId != 0;
        }

    }
}
