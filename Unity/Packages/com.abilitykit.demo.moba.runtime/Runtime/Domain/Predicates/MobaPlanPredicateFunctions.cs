using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Predicates
{
    public static class MobaPlanPredicateFunctions
    {
        public static readonly FunctionId HasBuffFunctionId = new FunctionId(StableStringId.Get("predicate:has_buff"));
        public static readonly FunctionId HasBuffOwnerFunctionId = new FunctionId(StableStringId.Get("predicate:has_buff_owner"));
        public static readonly FunctionId OwnerMatchesPayloadSourceFunctionId = new FunctionId(StableStringId.Get("predicate:owner_matches_payload_source"));
        public static readonly FunctionId OwnerMatchesPayloadTargetFunctionId = new FunctionId(StableStringId.Get("predicate:owner_matches_payload_target"));
        public static readonly FunctionId TargetIsFlyingProjectileFunctionId = new FunctionId(StableStringId.Get("predicate:target_is_flying_projectile"));

        public static void Register(FunctionRegistry functions)
        {
            if (functions == null) return;

            functions.Register<Predicate2<object, IWorldResolver>>(HasBuffFunctionId, HasBuff, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(HasBuffOwnerFunctionId, HasBuffOwner, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerMatchesPayloadSourceFunctionId, OwnerMatchesPayloadSource, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(OwnerMatchesPayloadTargetFunctionId, OwnerMatchesPayloadTarget, isDeterministic: true);
            functions.Register<Predicate2<object, IWorldResolver>>(TargetIsFlyingProjectileFunctionId, TargetIsFlyingProjectile, isDeterministic: true);
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
            var buffId = ReadInt(args, "0");
            if (buffId <= 0 || ctx.Context == null)
            {
                return false;
            }

            var checkStack = ReadInt(args, "1") != 0;
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

        private static int ReadInt(NamedArgsDict args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var value)) return 0;
            return (int)Math.Round(value.Ref.ConstValue);
        }
    }
}
