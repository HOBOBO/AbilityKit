using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 只解析动作执行所需的核心事实。
    /// 新增的动作专属需求应通过参数对象、负载提供者或专用动作输入解析器建模。
    /// </summary>
    internal static class MobaPlanActionInputResolver
    {
        public static bool TryResolve(object triggerArgs, ExecCtx<IWorldResolver> ctx, out MobaPlanActionInput input)
        {
            if (!MobaPlanActionExecutionContextResolver.TryResolve(triggerArgs, ctx, out var executionContext))
            {
                input = default;
                return false;
            }

            input = Create(triggerArgs, ctx, in executionContext);
            return true;
        }

        public static MobaPlanActionInput Resolve(object triggerArgs, ExecCtx<IWorldResolver> ctx)
        {
            if (TryResolve(triggerArgs, ctx, out var input))
            {
                return input;
            }

            var executionContext = MobaPlanActionExecutionContextResolver.Resolve(triggerArgs, ctx);
            return Create(triggerArgs, ctx, in executionContext);
        }

        public static bool TryResolveEffect(
            object triggerArgs,
            ExecCtx<IWorldResolver> ctx,
            out MobaEffectActionInput input)
        {
            if (!TryResolve(triggerArgs, ctx, out var core))
            {
                input = default;
                return false;
            }

            input = MobaPlanActionInputAssembler.AssembleEffect(in core);
            return true;
        }

        public static MobaEffectActionInput ResolveEffect(object triggerArgs, ExecCtx<IWorldResolver> ctx)
        {
            var core = Resolve(triggerArgs, ctx);
            return MobaPlanActionInputAssembler.AssembleEffect(in core);
        }

        public static MobaProjectileActionInput ResolveProjectile(object triggerArgs, ExecCtx<IWorldResolver> ctx)
        {
            var core = Resolve(triggerArgs, ctx);
            return MobaPlanActionInputAssembler.AssembleProjectile(in core);
        }

        public static bool TryResolveSummon(object triggerArgs, ExecCtx<IWorldResolver> ctx, out MobaSummonActionInput input)
        {
            if (!TryResolve(triggerArgs, ctx, out var core))
            {
                input = default;
                return false;
            }

            input = MobaPlanActionInputAssembler.AssembleSummon(in core, ctx);
            return true;
        }

        public static MobaSummonActionInput ResolveSummon(object triggerArgs, ExecCtx<IWorldResolver> ctx)
        {
            var core = Resolve(triggerArgs, ctx);
            return MobaPlanActionInputAssembler.AssembleSummon(in core, ctx);
        }

        private static MobaPlanActionInput Create(object triggerArgs, ExecCtx<IWorldResolver> ctx, in MobaCombatExecutionContext executionContext)
        {
            return MobaPlanActionInputAssembler.Assemble(triggerArgs, ctx, in executionContext);
        }
    }
}
