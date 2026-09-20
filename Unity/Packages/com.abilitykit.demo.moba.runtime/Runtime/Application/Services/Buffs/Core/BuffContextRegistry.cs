using System;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Core.Logging;
using AbilityKit.Effect;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Core {
    /// <summary>
    /// Buff 上下文注册器：trace 负责溯源链，runtime context 负责运行时值与快照生命周期。
    /// </summary>
    internal sealed class BuffContextRegistry
    {
        private readonly MobaTraceRegistry _trace;
        private readonly MobaRuntimeContextService _runtimeContexts;
        private readonly IFrameTime _frameTime;

        public BuffContextRegistry(MobaTraceRegistry trace, MobaRuntimeContextService runtimeContexts, IFrameTime frameTime)
        {
            _trace = trace;
            _runtimeContexts = runtimeContexts;
            _frameTime = frameTime;
        }

        /// <summary>
        /// 确保运行时拥有稳定的 trace/source 快照，并注册独立 runtime context 供触发器读取实时值。
        /// </summary>
        public void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId, in BuffOriginContext origin)
        {
            if (rt == null) return;

            var sourceOrigin = ResolveSourceOrigin(sourceActorId, targetActorId, buffId, in origin);
            var parentContextId = sourceOrigin.EffectiveParentContextId;
            var buffContextId = rt.SourceContextId;

            if (buffContextId == 0 && _trace != null)
            {
                buffContextId = parentContextId != 0
                    ? _trace.CreateChildContext(
                        parentContextId,
                        MobaTraceKind.BuffApply,
                        buffId,
                        sourceActorId,
                        targetActorId,
                        origin.ToOriginSourceEndpoint(),
                        origin.ToOriginTargetEndpoint())
                    : _trace.CreateRootContext(
                        MobaTraceKind.BuffApply,
                        buffId,
                        sourceActorId,
                        targetActorId,
                        origin.ToOriginSourceEndpoint(),
                        origin.ToOriginTargetEndpoint());
            }

            var buffOrigin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in sourceOrigin)
                .WithActors(sourceActorId, targetActorId)
                .WithImmediate(MobaTraceKind.BuffApply, buffId, buffContextId)
                .WithRootContext(sourceOrigin.EffectiveRootContextId != 0L ? sourceOrigin.EffectiveRootContextId : buffContextId)
                .WithOwnerContext(sourceOrigin.OwnerContextId != 0L ? sourceOrigin.OwnerContextId : buffContextId)
                .WithSkillRuntimeIfMissing(origin.SkillRuntimeHandle)
                .Build();

            rt.SourceContextId = buffContextId;
            rt.Origin = buffOrigin;
            rt.ContextSource = MobaContextSourceView.FromOrigin(
                in buffOrigin,
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Snapshot,
                false,
                "Buff",
                buffId);
            BindRuntimeContext(rt, targetActorId, MobaRuntimeContextLifecycleState.Active);
        }

        public void BindRuntimeContext(BuffRuntime rt, int targetActorId, MobaRuntimeContextLifecycleState state)
        {
            if (rt == null || _runtimeContexts == null) return;

            var frame = GetFrameOrDefault();
            var origin = rt.Origin;
            var data = new MobaBuffRuntimeContextData(
                rt.BuffId,
                rt.SourceId,
                targetActorId,
                rt.SourceContextId,
                origin.EffectiveRootContextId,
                origin.OwnerContextId,
                rt.StackCount,
                rt.Remaining,
                rt.IntervalRemainingSeconds,
                state,
                frame,
                rt.SkillRuntimeHandle);
            _runtimeContexts.EnsureBuffContext(rt, in data);
        }

        public void CancelAndEnd(BuffRuntime rt)
        {
            if (rt == null) return;

            if (rt.SourceContextId == 0)
            {
                DestroyRuntimeContext(rt, TraceLifecycleReason.Replaced);
                ClearSourceSnapshot(rt);
                return;
            }

            try
            {
                _trace?.EndContext(rt.SourceContextId, TraceLifecycleReason.Replaced);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextRegistry] Trace.End exception (sourceContextId={rt.SourceContextId})");
            }

            DestroyRuntimeContext(rt, TraceLifecycleReason.Replaced);
            ClearSourceSnapshot(rt);
        }

        public void EndByRuntime(BuffRuntime rt, TraceLifecycleReason reason)
        {
            if (rt == null) return;

            EndByRuntimeNoClear(rt, reason);
            ClearSourceSnapshot(rt);
        }

        /// <summary>
        /// 结束 trace 并取消 owner 动作，但保留运行时来源快照；移除阶段还需要用它发布事件/表现。
        /// </summary>
        public void EndByRuntimeNoClear(BuffRuntime rt, TraceLifecycleReason reason)
        {
            if (rt == null) return;

            if (rt.SourceContextId == 0)
            {
                DestroyRuntimeContext(rt, reason, preserveReference: true);
                return;
            }

            try
            {
                _trace?.EndContext(rt.SourceContextId, reason);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextRegistry] Trace.End exception (sourceContextId={rt.SourceContextId}, reason={reason})");
            }

            DestroyRuntimeContext(rt, reason, preserveReference: true);
        }

        private void DestroyRuntimeContext(BuffRuntime rt, TraceLifecycleReason reason, bool preserveReference = false)
        {
            if (rt == null || _runtimeContexts == null) return;

            var state = reason == TraceLifecycleReason.Replaced
                ? MobaRuntimeContextLifecycleState.Destroyed
                : MobaRuntimeContextLifecycleState.Ended;
            _runtimeContexts.SnapshotAndDestroyBuffContext(rt, state, GetFrameOrDefault(), preserveReference);
        }

        private static MobaGameplayOrigin ResolveSourceOrigin(int sourceActorId, int targetActorId, int buffId, in BuffOriginContext origin)
        {
            var hasOrigin = origin.TryGetOrigin(out var sourceOrigin);
            return hasOrigin
                ? sourceOrigin.WithActors(sourceActorId, targetActorId)
                : new MobaGameplayOrigin(
                    sourceActorId,
                    targetActorId,
                    MobaTraceKind.BuffApply,
                    buffId,
                    origin.ParentContextId != 0 ? origin.ParentContextId : origin.OriginContextId,
                    origin.ParentContextId != 0 ? origin.ParentContextId : origin.OriginContextId,
                    origin.ParentContextId != 0 ? origin.ParentContextId : origin.OriginContextId,
                    origin.ParentContextId != 0 ? origin.ParentContextId : origin.OriginContextId,
                    origin.SkillRuntimeHandle);
        }

        private static void ClearSourceSnapshot(BuffRuntime rt)
        {
            if (rt == null) return;
            rt.SourceContextId = 0;
            rt.RuntimeContextId = 0;
            rt.RuntimeContextVersion = 0;
            rt.RuntimeContextIdentity = default;
            rt.Origin = default;
            rt.ContextSource = default;
        }

        private int GetFrameOrDefault()
        {
            return _frameTime != null ? _frameTime.Frame.Value : 0;
        }
    }
}

