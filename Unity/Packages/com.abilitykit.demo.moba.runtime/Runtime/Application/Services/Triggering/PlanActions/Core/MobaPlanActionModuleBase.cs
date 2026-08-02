using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public interface IMobaPlanActionMetadata
    {
        string ActionName { get; }
    }

    /// <summary>
    /// Demo MOBA 强类型动作模块基类。
    /// 动作结构描述是 ActionId 的唯一来源，避免各个模块重复声明动作常量。
    /// </summary>
    public abstract class MobaPlanActionModuleBase<TActionArgs, TModule> : NamedArgsPlanActionModuleBase<TActionArgs, IWorldResolver, TModule>, IMobaPlanActionMetadata
        where TModule : MobaPlanActionModuleBase<TActionArgs, TModule>
    {
        protected sealed override ActionId ActionId => Schema.ActionId;

        public string ActionName => Schema is MobaPlanActionSchemaBase<TActionArgs> schema
            ? schema.ConfigActionName
            : null;

        protected bool TryResolveRequired<T>(ExecCtx<IWorldResolver> ctx, out T service)
            where T : class
        {
            return MobaPlanActionDiagnostics.TryResolveRequired(ctx.Context, ActionName ?? typeof(TModule).Name, out service);
        }

        protected void LogRejected(ExecCtx<IWorldResolver> ctx, string reason)
        {
            ctx.Control?.RejectAction(reason);
            MobaPlanActionDiagnostics.Rejected(ctx.Context, ActionName ?? typeof(TModule).Name, reason);
        }

        protected void LogApplied(ExecCtx<IWorldResolver> ctx, string message)
        {
            MobaPlanActionDiagnostics.Applied(ctx.Context, ActionName ?? typeof(TModule).Name, message);
        }

        protected void LogInvestigation(ExecCtx<IWorldResolver> ctx, string message)
        {
            MobaPlanActionDiagnostics.Investigation(ctx.Context, ActionName ?? typeof(TModule).Name, message);
        }

        protected void LogConfiguredActionDebug(ExecCtx<IWorldResolver> ctx, string message)
        {
            MobaPlanActionDiagnostics.ConfiguredActionDebug(ctx.Context, ActionName ?? typeof(TModule).Name, message);
        }

        protected void LogRejected(string reason)
        {
            MobaPlanActionDiagnostics.Rejected(ActionName ?? typeof(TModule).Name, reason);
        }

        protected void LogApplied(string message)
        {
            MobaPlanActionDiagnostics.Applied(ActionName ?? typeof(TModule).Name, message);
        }
    }
}
