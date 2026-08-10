using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.GameplayTags;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Buffs.Lifecycle
{
    /// <summary>
    /// Buff 应用流程：负责 Apply 阶段的配置校验、运行时选择、叠层执行、上下文/持续行为/触发计划绑定。
    /// </summary>
    internal sealed class BuffApplyFlow
    {
        private readonly MobaConfigDatabase _configs;
        private readonly MobaActorLookupService _actors;
        private readonly IMobaEffectiveTagQueryService _tags;
        private readonly IMobaContinuousTagTemplateRegistry _tagTemplates;
        private readonly BuffRepository _repo;
        private readonly BuffContextRegistry _ctx;
        private readonly BuffStackingPolicyApplier _stacking;
        private readonly BuffRuntimeBindingCoordinator _bindings;
        private readonly BuffEndFlow _endFlow;
        private readonly BuffLifecycleNotifier _notifier;

        public BuffApplyFlow(
            MobaConfigDatabase configs,
            MobaActorLookupService actors,
            IMobaEffectiveTagQueryService tags,
            IMobaContinuousTagTemplateRegistry tagTemplates,
            BuffRepository repo,
            BuffContextRegistry ctx,
            BuffStackingPolicyApplier stacking,
            BuffRuntimeBindingCoordinator bindings,
            BuffEndFlow endFlow,
            BuffLifecycleNotifier notifier)
        {
            _configs = configs;
            _actors = actors;
            _tags = tags;
            _tagTemplates = tagTemplates;
            _repo = repo ?? new BuffRepository();
            _ctx = ctx;
            _stacking = stacking ?? new BuffStackingPolicyApplier();
            _bindings = bindings;
            _endFlow = endFlow;
            _notifier = notifier;
        }

        public BuffLifecycleRejectResult LastReject { get; private set; }

        public bool Apply(in BuffApplyRequest request)
        {
            LastReject = BuffLifecycleRejectResult.None;
            if (!request.IsValid) return Reject(BuffLifecycleRejectCode.ApplyInvalidRequest, $"apply request is invalid. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId}.");
            if (_configs == null) return Reject(BuffLifecycleRejectCode.ApplyConfigDatabaseMissing, $"config database is missing. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId}.");
            if (!_configs.TryGetBuff(request.BuffId, out var buff) || buff == null) return Reject(BuffLifecycleRejectCode.ApplyConfigNotFound, $"buff config not found. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId}.");

            if (!TryGetTarget(request.TargetActorId, out var target)) return Reject(BuffLifecycleRejectCode.ApplyTargetNotFound, $"target actor not found. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId}.");

            var targetActorId = request.TargetActorId;
            var requirements = BuffTagLifecycle.ResolveRequirements(buff, _tagTemplates);
            if (!BuffTagLifecycle.CanActivate(_tags, targetActorId, requirements)) return Reject(BuffLifecycleRejectCode.ApplyTagRequirementsBlocked, $"tag requirements blocked activation. target={targetActorId} buffId={request.BuffId} source={request.SourceActorId}.");

            var list = _repo.GetOrCreateList(target);
            if (list == null) return Reject(BuffLifecycleRejectCode.ApplyRuntimeListUnavailable, $"buff runtime list is unavailable. target={targetActorId} buffId={request.BuffId} source={request.SourceActorId}.");

            var duration = request.DurationOverrideMs > 0 ? request.DurationOverrideMs : buff.DurationMs;
            var context = new BuffOperationContext
            {
                ApplyRequest = request,
                Buff = buff,
                TargetActorId = targetActorId,
                DurationSeconds = duration > 0 ? duration / 1000f : 0f,
                Requirements = requirements,
            };

            var existingKey = BuffRuntimeKey.MatchApplyRequest(in request);
            if (!request.ForceNewInstance && BuffRepository.TryGetRuntime(list, in existingKey, out var existingRuntime, out var existingIndex))
            {
                context.Runtime = existingRuntime;
                context.IsExistingRuntime = true;
                if (buff.StackingPolicy == BuffStackingPolicy.Replace)
                {
                    return ApplyReplacement(target, list, existingIndex, existingRuntime, ref context);
                }

                var applied = ApplyToExisting(target, ref context);
                if (applied) BuffRepository.MarkDirty(list);
                return applied;
            }

            return ApplyNew(target, list, ref context);
        }

        private bool ApplyToExisting(global::ActorEntity target, ref BuffOperationContext context)
        {
            var runtime = context.Runtime;
            var buff = context.Buff;
            var request = context.ApplyRequest;
            if (runtime == null) return Reject(BuffLifecycleRejectCode.ApplyExistingRuntimeMissing, "existing buff runtime is null.");
            if (buff == null) return Reject(BuffLifecycleRejectCode.ApplyConfigNotFound, "existing buff config is null.");

            var runtimeState = context.RuntimeView;
            var oldStackCount = runtime.StackCount;
            _ctx?.EnsureBuffContext(runtime, buff.Id, request.SourceActorId, context.TargetActorId, request.Origin);
            if (!_bindings.BindSkillRuntime(runtime, in request))
            {
                return Reject(BuffLifecycleRejectCode.ApplySkillRuntimeBindingFailed, $"skill runtime binding failed for existing buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={runtime.SourceContextId}.");
            }

            var stackingResult = _stacking.ApplyToExisting(runtime, buff, request.SourceActorId, context.DurationSeconds);
            var applied = stackingResult.Applied;
            _endFlow.NotifyLifecycle(runtime, MobaRuntimeLifecycleEventKind.Activated, "buff.lifecycle.active");
            if (applied || runtime.TagRequirements == null)
            {
                runtimeState.SetTagRequirements(context.Requirements);
            }

            if (applied && !EnsureContinuousRuntime(runtime, buff, request.SourceActorId, context.TargetActorId, context.DurationSeconds, context.Requirements))
            {
                return Reject(BuffLifecycleRejectCode.ApplyContinuousActivationFailed, $"continuous runtime activation failed for existing buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={runtime.SourceContextId}.");
            }

            if (stackingResult.ShouldResetInterval)
            {
                BuffStackingPolicyApplier.ResetInterval(runtime, buff);
            }

            _ctx?.BindRuntimeContext(runtime, context.TargetActorId, MobaRuntimeContextLifecycleState.Refreshed);
            _notifier.AppliedExisting(buff, request.SourceActorId, context.TargetActorId, context.DurationSeconds, runtime, oldStackCount, applied);
            return true;
        }

        private bool ApplyReplacement(
            global::ActorEntity target,
            List<BuffRuntime> list,
            int existingIndex,
            BuffRuntime existingRuntime,
            ref BuffOperationContext context)
        {
            var buff = context.Buff;
            var request = context.ApplyRequest;
            if (buff == null || existingRuntime == null)
            {
                return Reject(BuffLifecycleRejectCode.ApplyExistingRuntimeMissing, "replacement buff runtime or config is null.");
            }

            var replacement = _stacking.CreateNewRuntime(buff, request.SourceActorId, context.DurationSeconds);
            replacement.SourceContextId = request.SourceContextId;
            _ctx?.EnsureBuffContext(replacement, buff.Id, request.SourceActorId, context.TargetActorId, request.Origin);
            if (!_bindings.BindSkillRuntime(replacement, in request))
            {
                var failedSourceContextId = replacement.SourceContextId;
                CleanupFailedCandidate(target, context.TargetActorId, replacement);
                return Reject(BuffLifecycleRejectCode.ApplySkillRuntimeBindingFailed, $"skill runtime binding failed for replacement buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={failedSourceContextId}.");
            }

            new BuffRuntimeView(replacement).SetTagRequirements(context.Requirements);
            if (!EnsureContinuousRuntime(replacement, buff, request.SourceActorId, context.TargetActorId, context.DurationSeconds, context.Requirements))
            {
                var failedSourceContextId = replacement.SourceContextId;
                CleanupFailedCandidate(target, context.TargetActorId, replacement);
                return Reject(BuffLifecycleRejectCode.ApplyContinuousActivationFailed, $"continuous runtime activation failed for replacement buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={failedSourceContextId}.");
            }

            _ctx?.BindRuntimeContext(replacement, context.TargetActorId, MobaRuntimeContextLifecycleState.Active);
            if (!BuffRepository.ReplaceAt(list, existingIndex, existingRuntime, replacement))
            {
                var failedSourceContextId = replacement.SourceContextId;
                CleanupFailedCandidate(target, context.TargetActorId, replacement);
                return Reject(BuffLifecycleRejectCode.ApplyReplacementCommitFailed, $"replacement commit failed. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={failedSourceContextId}.");
            }

            var targetActorId = context.TargetActorId;
            var durationSeconds = context.DurationSeconds;
            Exception firstFailure = null;
            TryStep(() => _endFlow.EndCommittedRuntime(target, existingRuntime, existingRuntime.SourceId, TraceLifecycleReason.Replaced), ref firstFailure);
            TryStep(() => _endFlow.NotifyLifecycle(replacement, MobaRuntimeLifecycleEventKind.Activated, "buff.lifecycle.active"), ref firstFailure);
            TryStep(() => _notifier.AppliedNew(buff, request.SourceActorId, targetActorId, durationSeconds, replacement), ref firstFailure);
            if (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }

            return true;
        }

        private bool ApplyNew(global::ActorEntity target, List<BuffRuntime> list, ref BuffOperationContext context)
        {
            var buff = context.Buff;
            var request = context.ApplyRequest;
            if (buff == null) return Reject(BuffLifecycleRejectCode.ApplyConfigNotFound, "new buff config is null.");

            var runtime = _stacking.CreateNewRuntime(buff, request.SourceActorId, context.DurationSeconds);
            runtime.SourceContextId = request.SourceContextId;
            context.Runtime = runtime;
            _ctx?.EnsureBuffContext(runtime, buff.Id, request.SourceActorId, context.TargetActorId, request.Origin);
            if (!_bindings.BindSkillRuntime(runtime, in request))
            {
                var failedSourceContextId = runtime.SourceContextId;
                CleanupFailedCandidate(target, context.TargetActorId, runtime);
                return Reject(BuffLifecycleRejectCode.ApplySkillRuntimeBindingFailed, $"skill runtime binding failed for new buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={failedSourceContextId}.");
            }

            context.RuntimeView.SetTagRequirements(context.Requirements);
            if (!EnsureContinuousRuntime(runtime, buff, request.SourceActorId, context.TargetActorId, context.DurationSeconds, context.Requirements))
            {
                var failedSourceContextId = runtime.SourceContextId;
                CleanupFailedCandidate(target, context.TargetActorId, runtime);
                return Reject(BuffLifecycleRejectCode.ApplyContinuousActivationFailed, $"continuous runtime activation failed for new buff. target={context.TargetActorId} buffId={buff.Id} source={request.SourceActorId} sourceContextId={failedSourceContextId}.");
            }

            _ctx?.BindRuntimeContext(runtime, context.TargetActorId, MobaRuntimeContextLifecycleState.Active);
            list.Add(runtime);
            BuffRepository.RegisterRuntime(list, runtime);

            _endFlow.NotifyLifecycle(runtime, MobaRuntimeLifecycleEventKind.Activated, "buff.lifecycle.active");
            _notifier.AppliedNew(buff, request.SourceActorId, context.TargetActorId, context.DurationSeconds, runtime);
            return true;
        }

        private void CleanupFailedCandidate(global::ActorEntity target, int targetActorId, BuffRuntime runtime)
        {
            if (runtime == null) return;

            Exception firstFailure = null;
            TryStep(() => _bindings?.EndContinuous(runtime, TraceLifecycleReason.Failed), ref firstFailure);
            TryStep(() => _bindings?.CleanupContinuous(target, targetActorId, runtime, applyRemovalTags: false), ref firstFailure);
            TryStep(() => _ctx?.CancelAndEnd(runtime), ref firstFailure);
            TryStep(() => _endFlow.ReleaseSkillRuntime(runtime), ref firstFailure);
            TryStep(() => _endFlow.NotifyLifecycle(runtime, MobaRuntimeLifecycleEventKind.Failed, "buff.lifecycle.activateFailed"), ref firstFailure);
            TryStep(() => new BuffRuntimeView(runtime).ClearRuntimeBindings(), ref firstFailure);
            TryStep(() => BuffRepository.ReleaseRuntime(runtime), ref firstFailure);
            if (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }

        private bool EnsureContinuousRuntime(BuffRuntime runtime, BuffMO buff, int sourceActorId, int targetActorId, float remainingSeconds, ContinuousTagRequirements requirements)
        {
            return _bindings == null || _bindings.EnsureContinuousRuntime(runtime, buff, sourceActorId, targetActorId, remainingSeconds, requirements);
        }

        private bool TryGetTarget(int actorId, out global::ActorEntity target)
        {
            target = null;
            if (actorId <= 0) return false;
            if (_actors == null) return false;
            return _actors.TryGetActorEntity(actorId, out target) && target != null && target.hasActorId;
        }

        private static void TryStep(Action action, ref Exception firstFailure)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                if (firstFailure == null) firstFailure = ex;
            }
        }

        private bool Reject(BuffLifecycleRejectCode code, string message)
        {
            LastReject = new BuffLifecycleRejectResult(code, message);
            return false;
        }
    }
}
