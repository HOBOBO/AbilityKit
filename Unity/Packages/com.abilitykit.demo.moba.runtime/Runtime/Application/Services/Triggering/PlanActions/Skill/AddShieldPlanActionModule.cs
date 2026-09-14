using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AddShield)]
    public sealed class AddShieldPlanActionModule : MobaPlanActionModuleBase<AddShieldArgs, AddShieldPlanActionModule>
    {
        protected override IActionSchema<AddShieldArgs, IWorldResolver> Schema => AddShieldSchema.Instance;

        protected override void Execute(object triggerArgs, AddShieldArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryResolveRequired(ctx, out MobaShieldService shields))
            {
                return;
            }

            if (args.Value <= 0f && !args.Magnitude.Enabled)
            {
                LogRejected("requires positive shield value");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(
                    triggerArgs,
                    ctx,
                    out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var effectInput = new MobaEffectActionInput(in coreInput);
            var sourceActorId = effectInput.CasterActorId;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets))
                {
                    return;
                }

                var firstInstanceId = 0;
                var addedCount = 0;
                for (var i = 0; i < targets.Count; i++)
                {
                    var instanceId = AddShield(shields, args, effectInput, ctx, sourceActorId, targets[i], LogApplied);
                    if (instanceId <= 0) continue;
                    if (firstInstanceId == 0) firstInstanceId = instanceId;
                    addedCount++;
                }
                if (addedCount > 0 &&
                    (!MobaPlanActionOutput.TryWrite(in ctx, in args.ResultTarget, firstInstanceId, out var outputError) ||
                     !MobaPlanActionOutput.TryWrite(in ctx, in args.ResultCountTarget, addedCount, out outputError)))
                {
                    LogRejected(ctx, outputError);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static int AddShield(MobaShieldService shields, AddShieldArgs args, MobaEffectActionInput input, ExecCtx<IWorldResolver> ctx, int sourceActorId, int targetActorId, Action<string> logApplied)
        {
            if (targetActorId <= 0) return 0;

            var value = args.Value;
            var executionContext = input.ExecutionContext;
            if (args.Magnitude.Enabled && !MobaEffectMagnitudeResolver.TryEvaluate(
                    in args.Magnitude,
                    in executionContext,
                    in ctx,
                    sourceActorId,
                    input.CasterActorId,
                    targetActorId,
                    default,
                    out value,
                    out _,
                    out _)) return 0;
            if (value <= 0f) return 0;

            ResolveFrames(args, ctx, sourceActorId, targetActorId, out var startFrame, out var expireFrame);
            var origin = input.BuildOrigin(sourceActorId, targetActorId, MobaTraceKind.EffectExecution, args.ShieldId);
            var layer = new ShieldLayer
            {
                ShieldId = args.ShieldId,
                SourceActorId = sourceActorId,
                OwnerActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceContextId = origin.ImmediateContextId,
                RootContextId = origin.EffectiveRootContextId,
                OwnerContextId = origin.OwnerContextId,
                CurrentValue = MobaResourceFixedConvert.ToFixed(value),
                MaxValue = MobaResourceFixedConvert.ToFixed(value),
                InitialValue = MobaResourceFixedConvert.ToFixed(value),
                AbsorbRatio = MobaResourceFixedConvert.ToFixed(args.AbsorbRatio),
                Priority = args.Priority,
                DamageTypeMask = args.DamageTypeMask,
                StartFrame = startFrame,
                ExpireFrame = expireFrame,
                RemoveWhenDepleted = true,
                StackingPolicy = args.StackingPolicy,
                ConsumePolicy = args.ConsumePolicy,
                SharePolicy = ShieldSharePolicy.None,
                TransferPolicy = ShieldTransferPolicy.None,
            };

            var instanceId = shields.AddShield(targetActorId, layer);
            logApplied?.Invoke($"source={sourceActorId} target={targetActorId} shieldId={args.ShieldId} instance={instanceId} value={value:0.###} expireFrame={expireFrame}");
            return instanceId;
        }

        private static void ResolveFrames(AddShieldArgs args, ExecCtx<IWorldResolver> ctx, int sourceActorId, int targetActorId, out int startFrame, out int expireFrame)
        {
            var frameTime = ResolveFrameTime(ctx, sourceActorId, targetActorId, args.ShieldId);
            startFrame = frameTime.Frame.Value;
            expireFrame = 0;

            if (args.DurationFrames > 0)
            {
                expireFrame = startFrame + args.DurationFrames;
                return;
            }

            if (args.DurationMs > 0)
            {
                // FrameTime 走定点直达（不经 float Time 中转）；测试替身回退旧路径。
                var expire = frameTime is FrameTime fixedTime
                    ? fixedTime.FrameAfterSeconds(args.DurationMs / 1000f).Value
                    : frameTime.TimeToFrame(frameTime.Time + args.DurationMs / 1000f).Value;
                expireFrame = Math.Max(startFrame + 1, expire);
            }
        }

        private static IFrameTime ResolveFrameTime(ExecCtx<IWorldResolver> ctx, int sourceActorId, int targetActorId, int shieldId)
        {
            if (ctx.Context != null && ctx.Context.TryResolve<IFrameTime>(out var frameTime) && frameTime != null)
            {
                return frameTime;
            }

            throw new InvalidOperationException($"[Plan] add_shield requires IFrameTime for deterministic shield frame resolution. sourceActorId={sourceActorId}, targetActorId={targetActorId}, shieldId={shieldId}");
        }
    }
}
