using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Predicates
{
    public static class MobaPlanPredicateFunctions
    {
        public static readonly FunctionId HasQueryTargetFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.HasQueryTarget));
        public static readonly FunctionId HasBuffFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.HasBuff));
        public static readonly FunctionId HasBuffOwnerFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.HasBuffOwner));
        public static readonly FunctionId OwnerMatchesPayloadSourceFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.OwnerMatchesPayloadSource));
        public static readonly FunctionId OwnerMatchesPayloadTargetFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.OwnerMatchesPayloadTarget));
        public static readonly FunctionId OwnerSameTeamAsPayloadSourceFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.OwnerSameTeamAsPayloadSource));
        public static readonly FunctionId OwnerEnemyToPayloadTargetFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.OwnerEnemyToPayloadTarget));
        public static readonly FunctionId TargetIsFlyingProjectileFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.TargetIsFlyingProjectile));
        public static readonly FunctionId SourceTargetTagRulesFunctionId = new FunctionId(StableStringId.Get(CombatPredicateContracts.Function.SourceTargetTagRules));

        public static void Register(FunctionRegistry functions)
        {
            if (functions == null) return;

            functions.Register<Predicate2<object, IWorldResolver>>(HasQueryTargetFunctionId, HasQueryTarget, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(HasBuffFunctionId, HasBuff, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(HasBuffOwnerFunctionId, HasBuffOwner, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerMatchesPayloadSourceFunctionId, OwnerMatchesPayloadSource, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerMatchesPayloadTargetFunctionId, OwnerMatchesPayloadTarget, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerSameTeamAsPayloadSourceFunctionId, OwnerSameTeamAsPayloadSource, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerEnemyToPayloadTargetFunctionId, OwnerEnemyToPayloadTarget, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(TargetIsFlyingProjectileFunctionId, TargetIsFlyingProjectile, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(SourceTargetTagRulesFunctionId, SourceTargetTagRules, isDeterministic: true);
        }

        private static bool HasQueryTarget(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            var queryId = ReadInt(args, CombatPredicateContracts.Argument.FirstPosition);
            if (queryId <= 0 || ctx.Context == null ||
                !MobaPlanActionExecutionContextResolver.TryResolveExecutionScope(ctx, out _) ||
                !MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input))
                return false;

            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var config) ||
                !config.TryGetSearchQueryTemplate(queryId, out _))
                throw new InvalidOperationException($"Missing target query template for has_query_target. queryId={queryId}");

            var request = new MobaActionTargetRequest(MobaActionTargetSourceCode.SearchQueryTemplate,
                queryTemplateId: queryId);
            var effectInput = new MobaEffectActionInput(in input);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                return MobaActionTargetResolver.TryResolveTargetCollection(in request, in input,
                    in effectInput, ctx, CombatPredicateContracts.Type.HasQueryTarget, targets)
                    && targets.Count > 0;
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static bool SourceTargetTagRules(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            var sourceRuleGroupId = ReadInt(args, CombatPredicateContracts.Argument.FirstPosition);
            var targetRuleGroupId = ReadInt(args, CombatPredicateContracts.Argument.SecondPosition);
            if (sourceRuleGroupId <= 0 && targetRuleGroupId <= 0 || ctx.Context == null ||
                !ctx.Context.TryResolve<IMobaTagRuleQueryService>(out var rules) || rules == null)
                return false;

            if (sourceRuleGroupId > 0 &&
                (!CombatPredicateRuntime.TryResolveSourceActorId(triggerArgs, ctx.Context, out var sourceActorId) ||
                 !rules.TryMatchesRule(sourceActorId, sourceRuleGroupId, out var sourceMatches) || !sourceMatches))
                return false;
            if (targetRuleGroupId > 0 &&
                (!CombatPredicateRuntime.TryResolveTargetActorId(triggerArgs, ctx.Context, out var targetActorId) ||
                 !rules.TryMatchesRule(targetActorId, targetRuleGroupId, out var targetMatches) || !targetMatches))
                return false;
            return true;
        }

        private static bool TargetIsFlyingProjectile(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null
                || !CombatPredicateRuntime.TryResolveTargetActorId(triggerArgs, ctx.Context, out var actorId))
            {
                return false;
            }

            var actors = default(MobaActorLookupService);
            return CombatPredicateRuntime.TryGetActor(ctx.Context, ref actors, actorId, out var actor)
                && actor != null
                && actor.isFlyingProjectileTag;
        }

        private static bool HasBuff(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return HasBuffCore(triggerArgs, args, ctx, checkOwner: false);
        }

        private static bool HasBuffOwner(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return HasBuffCore(triggerArgs, args, ctx, checkOwner: true);
        }

        private static bool HasBuffCore(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx, bool checkOwner)
        {
            var buffId = ReadInt(args, CombatPredicateContracts.Argument.FirstPosition);
            if (buffId <= 0 || ctx.Context == null)
            {
                return false;
            }

            var checkStack = ReadInt(args, CombatPredicateContracts.Argument.SecondPosition) != 0;
            var actors = default(MobaActorLookupService);

            int actorId;
            if (checkOwner)
            {
                if (!CombatPredicateRuntime.TryResolveSourceActorId(triggerArgs, ctx.Context, out actorId))
                {
                    return false;
                }
            }
            else
            {
                if (!CombatPredicateRuntime.TryResolveTargetActorId(triggerArgs, ctx.Context, out actorId))
                {
                    return false;
                }
            }

            if (!CombatPredicateRuntime.TryGetActor(ctx.Context, ref actors, actorId, out var actor)
                || actor == null
                || !actor.hasBuffs
                || actor.buffs.Active == null)
            {
                return false;
            }

            var active = actor.buffs.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var runtime = active[i];
                if (runtime == null || runtime.BuffId != buffId) continue;
                return !checkStack || runtime.StackCount > 0;
            }
            return false;
        }

        private static bool OwnerMatchesPayloadSource(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return OwnerMatchesPayloadParticipant(triggerArgs, ctx, sourceParticipant: true);
        }

        private static bool OwnerMatchesPayloadTarget(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return OwnerMatchesPayloadParticipant(triggerArgs, ctx, sourceParticipant: false);
        }

        private static bool OwnerMatchesPayloadParticipant(object triggerArgs, ExecCtx<IWorldResolver> ctx, bool sourceParticipant)
        {
            if (ctx.Context == null
                || !ctx.Context.TryResolve<MobaOwnerBoundTriggerGateService>(out var gates)
                || gates == null
                || !gates.TryGetCurrentEvaluationSource(out var source))
            {
                return false;
            }

            var ownerActorId = source.SourceActorId;
            if (ownerActorId <= 0) return false;

            var payload = triggerArgs is MobaTriggerConditionContext conditionContext
                ? conditionContext.Payload
                : triggerArgs;
            if (payload is IMobaActorContextProvider actors)
            {
                var resolved = sourceParticipant
                    ? actors.TryGetSourceActorId(out var participantActorId)
                    : actors.TryGetTargetActorId(out participantActorId);
                return resolved && participantActorId == ownerActorId;
            }

            return false;
        }

        private static bool OwnerSameTeamAsPayloadSource(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return OwnerHasPayloadTeamRelation(triggerArgs, ctx, sourceParticipant: true, sameTeam: true);
        }

        private static bool OwnerEnemyToPayloadTarget(object triggerArgs, NamedArgsDict args, ExecCtx<IWorldResolver> ctx)
        {
            return OwnerHasPayloadTeamRelation(triggerArgs, ctx, sourceParticipant: false, sameTeam: false);
        }

        private static bool OwnerHasPayloadTeamRelation(
            object triggerArgs,
            ExecCtx<IWorldResolver> ctx,
            bool sourceParticipant,
            bool sameTeam)
        {
            if (ctx.Context == null
                || !ctx.Context.TryResolve<MobaOwnerBoundTriggerGateService>(out var gates)
                || gates == null
                || !gates.TryGetCurrentEvaluationSource(out var ownerSource)
                || ownerSource.SourceActorId <= 0
                || !ctx.Context.TryResolve<MobaCombatRulesService>(out var combatRules)
                || combatRules == null)
            {
                return false;
            }

            var payload = triggerArgs is MobaTriggerConditionContext conditionContext
                ? conditionContext.Payload
                : triggerArgs;
            if (!(payload is IMobaActorContextProvider actors)) return false;

            int participantActorId;
            var resolved = sourceParticipant
                ? actors.TryGetSourceActorId(out participantActorId)
                : actors.TryGetTargetActorId(out participantActorId);
            if (!resolved || participantActorId <= 0) return false;

            return sameTeam
                ? combatRules.AreSameTeam(ownerSource.SourceActorId, participantActorId)
                : combatRules.AreEnemies(ownerSource.SourceActorId, participantActorId);
        }

        private static int ReadInt(NamedArgsDict args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var value)) return 0;
            return (int)Math.Round(value.Ref.ConstValue);
        }
    }
}
