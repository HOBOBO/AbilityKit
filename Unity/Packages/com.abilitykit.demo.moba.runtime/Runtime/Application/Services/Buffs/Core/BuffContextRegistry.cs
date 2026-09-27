using System;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Effect;
using AbilityKit.Ability.FrameSync;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Core {
    /// <summary>
    /// Buff 上下文注册器：execution context 负责稳定身份与因果链，runtime context 负责运行时值与快照生命周期。
    /// </summary>
    internal sealed class BuffContextRegistry
    {
        private readonly MobaExecutionContextRegistry _executionContexts;
        private readonly MobaRuntimeContextService _runtimeContexts;
        private readonly IFrameTime _frameTime;

        public BuffContextRegistry(MobaExecutionContextRegistry executionContexts, MobaRuntimeContextService runtimeContexts, IFrameTime frameTime)
        {
            _executionContexts = executionContexts;
            _runtimeContexts = runtimeContexts;
            _frameTime = frameTime;
        }

        /// <summary>
        /// 确保运行时拥有稳定的执行/source 快照，并注册独立 runtime context 供触发器读取实时值。
        /// </summary>
        public void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId, in BuffOriginContext origin)
        {
            if (rt == null) return;

            var sourceOrigin = ResolveSourceOrigin(sourceActorId, targetActorId, buffId, in origin);
            var parentContextId = sourceOrigin.EffectiveParentContextId;
            var buffContextId = rt.SourceContextId;

            if (buffContextId == 0 && _executionContexts != null)
            {
                var created = _executionContexts.Create(new MobaExecutionContextCreateRequest(
                    MobaExecutionKind.BuffApply,
                    buffId,
                    sourceActorId,
                    targetActorId,
                    parentContextId,
                    sourceOrigin.EffectiveRootContextId,
                    sourceOrigin.OwnerContextId,
                    GetFrameOrDefault(),
                    originKind: ToExecutionKind(sourceOrigin.ImmediateKind),
                    originConfigId: sourceOrigin.ImmediateConfigId));
                buffContextId = created.ContextId;
            }

            var buffOrigin = MobaGameplayOriginBuilder.Create()
                .FromOrigin(in sourceOrigin)
                .WithActors(sourceActorId, targetActorId)
                .WithImmediate(MobaExecutionKind.BuffApply, buffId, buffContextId)
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
                DestroyRuntimeContext(rt, MobaExecutionEndReason.Replaced);
                ClearSourceSnapshot(rt);
                return;
            }

            _executionContexts?.End(
                rt.SourceContextId,
                (int)MobaExecutionEndReason.Replaced,
                GetFrameOrDefault());

            DestroyRuntimeContext(rt, MobaExecutionEndReason.Replaced);
            ClearSourceSnapshot(rt);
        }

        public void EndByRuntime(BuffRuntime rt, MobaExecutionEndReason reason)
        {
            if (rt == null) return;

            EndByRuntimeNoClear(rt, reason);
            ClearSourceSnapshot(rt);
        }

        /// <summary>
        /// 结束执行上下文并取消 owner 动作，但保留运行时来源快照；移除阶段还需要用它发布事件/表现。
        /// </summary>
        public void EndByRuntimeNoClear(BuffRuntime rt, MobaExecutionEndReason reason)
        {
            if (rt == null) return;

            if (rt.SourceContextId == 0)
            {
                DestroyRuntimeContext(rt, reason, preserveReference: true);
                return;
            }

            _executionContexts?.End(rt.SourceContextId, (int)reason, GetFrameOrDefault());

            DestroyRuntimeContext(rt, reason, preserveReference: true);
        }

        private void DestroyRuntimeContext(BuffRuntime rt, MobaExecutionEndReason reason, bool preserveReference = false)
        {
            if (rt == null || _runtimeContexts == null) return;

            var state = reason == MobaExecutionEndReason.Replaced
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
                    MobaExecutionKind.BuffApply,
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

        private static MobaExecutionKind ToExecutionKind(MobaExecutionKind kind)
        {
            switch (kind)
            {
                case MobaExecutionKind.SkillCast: return MobaExecutionKind.SkillCast;
                case MobaExecutionKind.SkillEffect: return MobaExecutionKind.SkillEffect;
                case MobaExecutionKind.SkillPhase: return MobaExecutionKind.SkillPhase;
                case MobaExecutionKind.EffectExecution: return MobaExecutionKind.EffectExecution;
                case MobaExecutionKind.EffectAction: return MobaExecutionKind.EffectAction;
                case MobaExecutionKind.BuffApply: return MobaExecutionKind.BuffApply;
                case MobaExecutionKind.BuffTick: return MobaExecutionKind.BuffTick;
                case MobaExecutionKind.BuffRemove: return MobaExecutionKind.BuffRemove;
                case MobaExecutionKind.ProjectileLaunch: return MobaExecutionKind.ProjectileLaunch;
                case MobaExecutionKind.ProjectileHit: return MobaExecutionKind.ProjectileHit;
                case MobaExecutionKind.AreaSpawn: return MobaExecutionKind.AreaSpawn;
                case MobaExecutionKind.AreaEnter: return MobaExecutionKind.AreaEnter;
                case MobaExecutionKind.AreaExit: return MobaExecutionKind.AreaExit;
                case MobaExecutionKind.AreaExpire: return MobaExecutionKind.AreaExpire;
                case MobaExecutionKind.AreaStay: return MobaExecutionKind.AreaStay;
                case MobaExecutionKind.SummonSpawn: return MobaExecutionKind.SummonSpawn;
                case MobaExecutionKind.SummonDeath: return MobaExecutionKind.SummonDeath;
                case MobaExecutionKind.UnitSpawn: return MobaExecutionKind.UnitSpawn;
                case MobaExecutionKind.UnitDespawn: return MobaExecutionKind.UnitDespawn;
                case MobaExecutionKind.UnitDeath: return MobaExecutionKind.UnitDeath;
                case MobaExecutionKind.UnitRespawn: return MobaExecutionKind.UnitRespawn;
                case MobaExecutionKind.DamageAttack: return MobaExecutionKind.DamageAttack;
                case MobaExecutionKind.DamageCalc: return MobaExecutionKind.DamageCalc;
                case MobaExecutionKind.DamageApply: return MobaExecutionKind.DamageApply;
                case MobaExecutionKind.PresentationPlay: return MobaExecutionKind.PresentationPlay;
                case MobaExecutionKind.PresentationStop: return MobaExecutionKind.PresentationStop;
                default: return MobaExecutionKind.None;
            }
        }
    }
}

