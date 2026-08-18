using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.GameplayTags;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;

namespace AbilityKit.Demo.Moba.Services.Buffs.Lifecycle {
    /// <summary>
    /// 生命周期拒绝原因快照，用于把底层失败原因反馈给入口服务和诊断系统。
    /// </summary>
    internal readonly struct BuffLifecycleRejectResult
    {
        public static readonly BuffLifecycleRejectResult None = new BuffLifecycleRejectResult(BuffLifecycleRejectCode.None, null);

        public BuffLifecycleRejectResult(BuffLifecycleRejectCode kind, string message)
        {
            Kind = kind;
            Code = BuffLifecycleRejectCodes.ToCode(kind);
            Message = message;
        }

        public BuffLifecycleRejectResult(string code, string message)
        {
            Kind = BuffLifecycleRejectCode.LifecycleRejected;
            Code = code;
            Message = message;
        }

        public BuffLifecycleRejectCode Kind { get; }
        public string Code { get; }
        public string Message { get; }
        public bool HasValue => Kind != BuffLifecycleRejectCode.None || !string.IsNullOrEmpty(Code) || !string.IsNullOrEmpty(Message);

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Code)) return string.IsNullOrEmpty(Message) ? "unknown" : Message;
            if (string.IsNullOrEmpty(Message)) return Code;
            return Code + ": " + Message;
        }
    }

    /// <summary>
    /// Buff 生命周期编排器：集中处理配置校验、叠层策略、上下文创建、持续行为绑定、事件/表现派发和最终清理。
    /// </summary>
    internal sealed class BuffLifecycleExecutor
    {
        private readonly MobaActorLookupService _actors;
        private readonly BuffEndFlow _endFlow;
        private readonly BuffApplyFlow _applyFlow;

        public BuffLifecycleRejectResult LastReject { get; private set; }
        public string LastRejectReason => LastReject.Message;

        public BuffLifecycleExecutor(
            MobaConfigDatabase configs,
            MobaActorLookupService actors,
            MobaRuntimeLifecycleHookService lifecycleHooks,
            IMobaEffectiveTagQueryService tags,
            IMobaContinuousTagTemplateRegistry tagTemplates,
            BuffRepository repo,
            BuffContextRegistry ctx,
            BuffEventPublisher events,
            BuffStageEffectExecutor stageEffects,
            BuffLifecycleNotifier notifier,
            BuffStackingPolicyApplier stacking,
            BuffContinuousBindingService continuousBindings,
            MobaSkillCastRuntimeService skillRuntimes,
            MobaBuffPresentationCueReporter presentationCues,
            BuffEndFlow endFlow)
        {
            _actors = actors;
            var resolvedRepo = repo ?? new BuffRepository();
            var resolvedNotifier = notifier ?? new BuffLifecycleNotifier(events, stageEffects, presentationCues);
            var resolvedStacking = stacking ?? new BuffStackingPolicyApplier();
            var resolvedBindings = new BuffRuntimeBindingCoordinator(lifecycleHooks, continuousBindings, skillRuntimes);
            _endFlow = endFlow ?? new BuffEndFlow(configs, ctx, resolvedNotifier, resolvedBindings);
            _applyFlow = new BuffApplyFlow(configs, actors, tags, tagTemplates, resolvedRepo, ctx, resolvedStacking, resolvedBindings, _endFlow, resolvedNotifier);
        }

        /// <summary>
        /// 应用 Buff 的主流程。先校验配置和标签门禁，再决定刷新已有实例或创建新实例。
        /// </summary>
        public bool Apply(in BuffApplyRequest request)
        {
            var applied = _applyFlow.Apply(in request);
            LastReject = _applyFlow.LastReject;
            return applied;
        }

        /// <summary>
        /// 移除 Buff 的主流程。倒序遍历可安全处理同一列表内的多个匹配运行时。
        /// </summary>
        public bool Remove(in BuffRemoveRequest request)
        {
            LastReject = BuffLifecycleRejectResult.None;
            if (!request.IsValid) return Reject(BuffLifecycleRejectCode.RemoveInvalidRequest, $"remove request is invalid. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} reason={request.Reason}.");
            if (!TryGetTarget(request.TargetActorId, out var target)) return Reject(BuffLifecycleRejectCode.RemoveTargetNotFound, $"target actor not found. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} reason={request.Reason}.");
            if (!target.hasBuffs) return Reject(BuffLifecycleRejectCode.RemoveBuffsComponentMissing, $"target has no buffs component. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} reason={request.Reason}.");

            var list = target.buffs.Active;
            if (list == null || list.Count == 0) return Reject(BuffLifecycleRejectCode.RemoveNoActiveRuntimes, $"target has no active buff runtimes. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} reason={request.Reason}.");

            var removed = false;
            var normalizedReason = NormalizeRemoveReason(request.Reason);
            var key = BuffRuntimeKey.MatchRemoveRequest(in request);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var runtime = list[i];
                if (!key.Matches(runtime)) continue;

                removed |= EndRuntime(target, list, i, runtime, request.SourceActorId > 0 ? request.SourceActorId : runtime.SourceId, normalizedReason);
            }

            if (!removed) return Reject(BuffLifecycleRejectCode.RemoveRuntimeNotFound, $"buff runtime not found. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} reason={request.Reason}.");
            return true;
        }

        private bool Reject(BuffLifecycleRejectCode code, string message)
        {
            LastReject = new BuffLifecycleRejectResult(code, message);
            return false;
        }

        /// <summary>
        /// 结束单个 Buff 运行时。先从仓库提交移除，再清理绑定、发布提交后事件并回收到对象池。
        /// </summary>
        public bool EndRuntime(global::ActorEntity target, List<BuffRuntime> list, int index, BuffRuntime runtime, int sourceActorId, TraceLifecycleReason reason)
        {
            return _endFlow.EndRuntime(target, list, index, runtime, sourceActorId, reason);
        }

        private bool TryGetTarget(int actorId, out global::ActorEntity target)
        {
            target = null;
            if (actorId <= 0) return false;
            if (_actors == null) return false;
            return _actors.TryGetActorEntity(actorId, out target) && target != null && target.hasActorId;
        }

        private static TraceLifecycleReason NormalizeRemoveReason(TraceLifecycleReason reason)
        {
            return reason == TraceLifecycleReason.None ? TraceLifecycleReason.Dispelled : reason;
        }

    }

    internal static class BuffLifecycleExecutorFactory
    {
        public static BuffLifecycleExecutor Create(IWorldResolver services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services), "Buff lifecycle executor requires a world service resolver.");
            }

            services.TryResolve(out MobaConfigDatabase configs);
            services.TryResolve(out AbilityKit.Triggering.Eventing.IEventBus eventBus);
            services.TryResolve(out ITriggerActionRunner actionRunner);
            services.TryResolve(out MobaTraceRegistry trace);
            services.TryResolve(out MobaEffectExecutionService effects);
            services.TryResolve(out IMobaEffectiveTagQueryService tags);
            services.TryResolve(out IMobaContinuousTagTemplateRegistry tagTemplates);
            services.TryResolve(out IFrameTime frameTime);
            services.TryResolve(out IContinuousManager continuous);
            services.TryResolve(out MobaActorLookupService actors);
            services.TryResolve(out MobaSkillCastRuntimeService skillRuntimes);
            services.TryResolve(out MobaPresentationCueSnapshotService cueSnapshots);
            services.TryResolve(out MobaRuntimeContextService runtimeContexts);
            services.TryResolve(out IMobaBattleDiagnosticEventSink diagnostics);
  
            services.TryResolve(out AbilityKit.Demo.Moba.Services.Triggering.MobaTriggerPlanSubscriptionService triggerSubscriptions);
            services.TryResolve(out AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.MobaTriggerExecutionGateway triggerGateway);
            if (triggerGateway == null) triggerGateway = new AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.MobaTriggerExecutionGateway(effects, triggerSubscriptions);
 
            var repo = new BuffRepository();
            var ctx = new BuffContextRegistry(trace, runtimeContexts, actionRunner, frameTime);
            var events = new BuffEventPublisher(eventBus);
            var stageEffects = new BuffStageEffectExecutor(triggerGateway);
            var stacking = new BuffStackingPolicyApplier();
            var presentationCues = new MobaBuffPresentationCueReporter(configs, cueSnapshots);
            var continuousBindings = new BuffContinuousBindingService(continuous, tags);
            var notifier = new BuffLifecycleNotifier(events, stageEffects, presentationCues, diagnostics);

            var lifecycleHooks = MobaRuntimeLifecycleHookFactory.CreateDefault(trace);
            var bindings = new BuffRuntimeBindingCoordinator(lifecycleHooks, continuousBindings, skillRuntimes);
            var endFlow = new BuffEndFlow(configs, ctx, notifier, bindings);
 
            return new BuffLifecycleExecutor(
                configs,
                actors,
                lifecycleHooks,
                tags,
                tagTemplates,
                repo,
                ctx,
                events,
                stageEffects,
                notifier,
                stacking,
                continuousBindings,
                skillRuntimes,
                presentationCues,
                endFlow);
        }
    }
}

