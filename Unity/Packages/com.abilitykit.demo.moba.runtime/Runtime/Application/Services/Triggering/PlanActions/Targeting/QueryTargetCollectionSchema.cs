using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class QueryTargetCollectionSchema : MobaPlanActionSchemaBase<QueryTargetCollectionArgs>
    {
        public static readonly QueryTargetCollectionSchema Instance = new QueryTargetCollectionSchema();

        protected override string ActionName => TriggeringConstants.Actions.QueryTargetCollection;

        public override QueryTargetCollectionArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            return ParseArgs(namedArgs, ctx, default);
        }

        public override QueryTargetCollectionArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx,
            in TriggerActionParseContext parseContext)
        {
            var request = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            TryReadBlackboardTarget(namedArgs, out var resultTarget, "result", "collection");
            return new QueryTargetCollectionArgs(in request, in resultTarget);
        }

        public override bool TryValidateArgs(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            out string error)
        {
            return RequireBlackboardTarget(args, "result", out error, "result", "collection");
        }
    }
}
