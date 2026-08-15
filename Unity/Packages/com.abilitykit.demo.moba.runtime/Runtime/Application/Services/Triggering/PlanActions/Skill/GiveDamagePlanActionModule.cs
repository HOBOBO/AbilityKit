using System.Collections.Generic;
using System.Text;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using CritType = AbilityKit.Demo.Moba.CritType;
using DamageReasonKind = AbilityKit.Demo.Moba.DamageReasonKind;
using DamageFormulaKind = AbilityKit.Demo.Moba.DamageFormulaKind;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 造成伤害的 Plan Action 模块，使用强类型命名参数 Schema API。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.GiveDamage)]
    public sealed class GiveDamagePlanActionModule : MobaPlanActionModuleBase<GiveDamageArgs, GiveDamagePlanActionModule>
    {
        protected override IActionSchema<GiveDamageArgs, IWorldResolver> Schema => GiveDamageSchema.Instance;

        protected override void Execute(object triggerArgs, GiveDamageArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaCombatEffectService>(out var combat) || combat == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.GiveDamage, "cannot resolve MobaCombatEffectService.");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var effectInput = new MobaEffectActionInput(in coreInput);
            if (!effectInput.HasCasterActor)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.GiveDamage, $"missing caster. target={effectInput.TargetActorId}, damage={args.DamageValue:0.###}, reasonParam={args.ReasonParam}");
                return;
            }

            var attackerActorId = effectInput.CasterActorId;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.GiveDamage, targets))
                {
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    ExecuteDamage(combat, args, effectInput, ctx, attackerActorId, targets[i]);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static void ExecuteDamage(MobaCombatEffectService combat, GiveDamageArgs args, MobaEffectActionInput input, ExecCtx<IWorldResolver> ctx, int attackerActorId, int targetActorId)
        {
            if (targetActorId <= 0)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.GiveDamage, $"invalid target. attacker={attackerActorId}, target={targetActorId}, damage={args.DamageValue:0.###}, reasonParam={args.ReasonParam}");
                return;
            }

            var origin = input.BuildOrigin(attackerActorId, targetActorId, MobaTraceKind.EffectExecution, 0);
            var requestedDamage = ResolveRequestedDamage(args, ctx, attackerActorId);
            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = args.DamageType,
                CritType = CritType.None,
                ReasonKind = args.ReasonKind,
                ReasonParam = args.ReasonParam,
                FormulaKind = (int)DamageFormulaKind.Standard,
            };
            attack.SetOrigin(in origin);
            attack.BaseDamage.FixedBaseValue = requestedDamage;

            var result = combat.DealDamage(attack);
            if (result == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.GiveDamage, $"pipeline returned null. attacker={attackerActorId} target={targetActorId} damage={MobaResourceFixedConvert.ToSingle(requestedDamage):0.###} reasonParam={args.ReasonParam}");
                return;
            }

            LogDamageTrace(args, input, ctx, MobaResourceFixedConvert.ToSingle(requestedDamage), attack.BaseDamage.Value, in origin, result);
        }

        private static Fixed64 ResolveRequestedDamage(GiveDamageArgs args, ExecCtx<IWorldResolver> ctx, int attackerActorId)
        {
            if (args.SourceAttackRatio == 0f || attackerActorId <= 0) return MobaResourceFixedConvert.ToFixed(args.DamageValue);
            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null) return MobaResourceFixedConvert.ToFixed(args.DamageValue);
            if (!actors.TryGetActorEntity(attackerActorId, out var attacker) || attacker == null || !attacker.hasAttributeGroup) return MobaResourceFixedConvert.ToFixed(args.DamageValue);

            var attrs = attacker.GetMobaAttrs();
            var sourceAttack = MobaResourceFixedConvert.ToFixed(args.DamageType == DamageType.Magic ? attrs.MagicAttack : attrs.PhysicsAttack);
            return MobaResourceFixedConvert.ToFixed(args.DamageValue) + sourceAttack * MobaResourceFixedConvert.ToFixed(args.SourceAttackRatio);
        }

        private static void LogDamageTrace(GiveDamageArgs args, MobaEffectActionInput input, ExecCtx<IWorldResolver> ctx, float requestedDamage, float pipelineBaseDamage, in MobaGameplayOrigin origin, DamageResult result)
        {
            var sb = new StringBuilder(1024);
            sb.Append("[MobaDamageTrace] damage applied")
                .Append(" attacker=").Append(result.AttackerActorId)
                .Append(" target=").Append(result.TargetActorId)
                .Append(" requested=").Append(requestedDamage.ToString("0.###"))
                .Append(" pipelineBase=").Append(pipelineBaseDamage.ToString("0.###"))
                .Append(" actual=").Append(result.Value.ToString("0.###"))
                .Append(" damageType=").Append(result.DamageType)
                .Append(" reason=").Append(result.ReasonKind).Append(':').Append(result.ReasonParam)
                .Append(" originKind=").Append(origin.ImmediateKind)
                .Append(" originConfig=").Append(origin.ImmediateConfigId)
                .Append(" immediateCtx=").Append(origin.ImmediateContextId)
                .Append(" parentCtx=").Append(origin.EffectiveParentContextId)
                .Append(" rootCtx=").Append(origin.EffectiveRootContextId)
                .Append(" ownerCtx=").Append(origin.OwnerContextId)
                .Append(" skillHandle=").Append(origin.SkillRuntimeHandle.ToString());

            AppendSkillRuntime(sb, ctx, in origin);
            AppendTraceChain(sb, ctx, input, in origin);

            MobaPlanActionDiagnostics.Investigation(ctx.Context, TriggeringConstants.Actions.GiveDamage, sb.ToString());
        }

        private static void AppendSkillRuntime(StringBuilder sb, ExecCtx<IWorldResolver> ctx, in MobaGameplayOrigin origin)
        {
            var handle = origin.SkillRuntimeHandle;
            if (!handle.IsValid)
            {
                sb.AppendLine().Append("  skillRuntime: missing handle");
                return;
            }

            if (!ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null)
            {
                sb.AppendLine().Append("  skillRuntime: service not resolved handle=").Append(handle.ToString());
                return;
            }

            if (!runtimes.TryGet(in handle, out var runtime) || runtime == null)
            {
                sb.AppendLine().Append("  skillRuntime: not found handle=").Append(handle.ToString())
                    .Append(" rootTrace=").Append(handle.RootTraceContextId);
                return;
            }

            sb.AppendLine().Append("  skillRuntime: skillId=").Append(runtime.SkillId)
                .Append(" slot=").Append(runtime.SkillSlot)
                .Append(" level=").Append(runtime.SkillLevel)
                .Append(" runtime=").Append(runtime.RuntimeId).Append(':').Append(runtime.Generation)
                .Append(" stage=").Append(runtime.Stage)
                .Append(" caster=").Append(runtime.CasterActorId)
                .Append(" target=").Append(runtime.TargetActorId)
                .Append(" rootTrace=").Append(runtime.RootTraceContextId)
                .Append(" pendingChildren=").Append(runtime.PendingChildren);

            var children = runtime.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                sb.AppendLine().Append("    child[").Append(i).Append("]: kind=").Append(child.Kind)
                    .Append(" id=").Append(child.ChildId)
                    .Append(" traceCtx=").Append(child.TraceContextId)
                    .Append(" config=").Append(child.ConfigId);
            }
        }

        private static void AppendTraceChain(StringBuilder sb, ExecCtx<IWorldResolver> ctx, MobaEffectActionInput input, in MobaGameplayOrigin origin)
        {
            if (!ctx.Context.TryResolve<MobaTraceRegistry>(out var traces) || traces == null)
            {
                sb.AppendLine().Append("  traceChain: registry not resolved");
                return;
            }

            var rootId = ResolveTraceRootId(input, in origin);
            if (rootId == 0L)
            {
                sb.AppendLine().Append("  traceChain: missing root id");
                return;
            }

            var chain = traces.GetChain(rootId);
            if (chain == null || chain.Count == 0)
            {
                sb.AppendLine().Append("  traceChain: empty root=").Append(rootId);
                return;
            }

            sb.AppendLine().Append("  traceChain: root=").Append(rootId).Append(" nodes=").Append(chain.Count);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = chain[i];
                var metadata = node.Metadata != null ? node.Metadata.ToDisplayString() : string.Empty;
                sb.AppendLine().Append("    [").Append(i).Append("] kind=").Append((MobaTraceKind)node.Kind)
                    .Append(" ctx=").Append(node.ContextId)
                    .Append(" parent=").Append(node.ParentId)
                    .Append(" childCount=").Append(node.ChildCount);

                if (!string.IsNullOrEmpty(metadata))
                {
                    sb.Append(" meta=").Append(metadata);
                }
            }
        }

        private static long ResolveTraceRootId(MobaEffectActionInput input, in MobaGameplayOrigin origin)
        {
            if (origin.EffectiveRootContextId != 0L) return origin.EffectiveRootContextId;
            if (origin.SkillRuntimeHandle.RootTraceContextId != 0L) return origin.SkillRuntimeHandle.RootTraceContextId;

            var executionContext = input.ExecutionContext;
            if (executionContext.RootContextId != 0L) return executionContext.RootContextId;
            if (executionContext.SkillRuntimeHandle.RootTraceContextId != 0L) return executionContext.SkillRuntimeHandle.RootTraceContextId;

            return 0L;
        }
    }
}
