using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.GameplayTags;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Core {
    /// <summary>
    /// Buff 来源上下文：保留父级 trace、来源/目标 Actor、技能运行时句柄，用于后续事件和效果溯源。
    /// </summary>
    public readonly struct BuffOriginContext : IMobaOriginContextProvider
    {
        public readonly long ParentContextId;
        public readonly int OriginSourceActorId;
        public readonly int OriginTargetActorId;
        public readonly MobaTraceKind OriginKind;
        public readonly int OriginConfigId;
        public readonly long OriginContextId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;
        public readonly MobaGameplayOrigin Origin;

        public BuffOriginContext(long parentContextId, int originSourceActorId, int originTargetActorId, MobaTraceKind originKind = MobaTraceKind.None, int originConfigId = 0, long originContextId = 0, MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            ParentContextId = parentContextId;
            OriginSourceActorId = originSourceActorId;
            OriginTargetActorId = originTargetActorId;
            OriginKind = originKind;
            OriginConfigId = originConfigId;
            OriginContextId = originContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            Origin = new MobaGameplayOrigin(
                originSourceActorId,
                originTargetActorId,
                originKind,
                originConfigId,
                parentContextId != 0 ? parentContextId : originContextId,
                parentContextId != 0 ? parentContextId : originContextId,
                parentContextId != 0 ? parentContextId : originContextId,
                parentContextId != 0 ? parentContextId : originContextId,
                skillRuntimeHandle);
        }

        public BuffOriginContext(in MobaGameplayOrigin origin)
        {
            Origin = origin;
            ParentContextId = origin.EffectiveParentContextId;
            OriginSourceActorId = origin.SourceActorId;
            OriginTargetActorId = origin.TargetActorId;
            OriginKind = origin.ImmediateKind;
            OriginConfigId = origin.ImmediateConfigId;
            OriginContextId = origin.ImmediateContextId;
            SkillRuntimeHandle = origin.SkillRuntimeHandle;
        }

        public static BuffOriginContext FromActors(long parentContextId, int originSourceActorId, int originTargetActorId)
        {
            return new BuffOriginContext(parentContextId, originSourceActorId, originTargetActorId);
        }

        public static BuffOriginContext FromActors(long parentContextId, int originSourceActorId, int originTargetActorId, in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            return new BuffOriginContext(parentContextId, originSourceActorId, originTargetActorId, skillRuntimeHandle: skillRuntimeHandle);
        }

        public static BuffOriginContext FromOrigin(in MobaGameplayOrigin origin)
        {
            return new BuffOriginContext(in origin);
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = Origin.IsValid
                ? Origin
                : new MobaGameplayOrigin(
                    OriginSourceActorId,
                    OriginTargetActorId,
                    OriginKind,
                    OriginConfigId,
                    ParentContextId != 0 ? ParentContextId : OriginContextId,
                    ParentContextId != 0 ? ParentContextId : OriginContextId,
                    ParentContextId != 0 ? ParentContextId : OriginContextId,
                    ParentContextId != 0 ? ParentContextId : OriginContextId,
                    SkillRuntimeHandle);
            return origin.IsValid;
        }

        public TraceEndpoint ToOriginSourceEndpoint()
        {
            return TraceEndpoint.Actor(OriginSourceActorId);
        }

        public TraceEndpoint ToOriginTargetEndpoint()
        {
            return TraceEndpoint.Actor(OriginTargetActorId);
        }
    }

    internal struct BuffApplyRequest
    {
        public int TargetActorId;
        public int BuffId;
        public int SourceActorId;
        public int DurationOverrideMs;
        public long SourceContextId;
        public bool ForceNewInstance;
        public BuffOriginContext Origin;
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle => Origin.SkillRuntimeHandle;

        public bool IsValid => TargetActorId > 0 && BuffId > 0;
    }

    internal struct BuffRemoveRequest
    {
        public int TargetActorId;
        public int BuffId;
        public int SourceActorId;
        public long SourceContextId;
        public TraceLifecycleReason Reason;

        public bool IsValid => TargetActorId > 0 && BuffId > 0;
    }

    /// <summary>
    /// Buff 运行时匹配键。普通 apply 按 buff/source 匹配；sourceContextId 非零时按显式实例匹配。
    /// </summary>
    internal readonly struct BuffRuntimeKey
    {
        public readonly int BuffId;
        public readonly int SourceActorId;
        public readonly long SourceContextId;

        private BuffRuntimeKey(int buffId, int sourceActorId, long sourceContextId)
        {
            BuffId = buffId;
            SourceActorId = sourceActorId;
            SourceContextId = sourceContextId;
        }

        public static BuffRuntimeKey MatchBuff(int buffId)
        {
            return new BuffRuntimeKey(buffId, 0, 0L);
        }

        public static BuffRuntimeKey MatchBuffAndSource(int buffId, int sourceActorId)
        {
            return new BuffRuntimeKey(buffId, sourceActorId, 0L);
        }

        public static BuffRuntimeKey MatchInstance(int buffId, int sourceActorId, long sourceContextId)
        {
            return new BuffRuntimeKey(buffId, sourceActorId, sourceContextId);
        }

        public static BuffRuntimeKey MatchApplyRequest(in BuffApplyRequest request)
        {
            if (request.SourceContextId != 0L) return MatchInstance(request.BuffId, request.SourceActorId, request.SourceContextId);
            return MatchBuffAndSource(request.BuffId, request.SourceActorId);
        }

        public static BuffRuntimeKey MatchRemoveRequest(in BuffRemoveRequest request)
        {
            if (request.SourceContextId != 0L) return MatchInstance(request.BuffId, request.SourceActorId, request.SourceContextId);
            if (request.SourceActorId > 0) return MatchBuffAndSource(request.BuffId, request.SourceActorId);
            return MatchBuff(request.BuffId);
        }

        public bool Matches(BuffRuntime runtime)
        {
            if (runtime == null) return false;
            if (BuffId > 0 && runtime.BuffId != BuffId) return false;
            if (SourceActorId > 0 && runtime.SourceId != SourceActorId) return false;
            if (SourceContextId != 0L && runtime.SourceContextId != SourceContextId) return false;
            return true;
        }
    }

    /// <summary>
    /// 单次 apply/remove 流程内共享的临时上下文，避免各阶段重复传递大量参数。
    /// </summary>
    internal struct BuffOperationContext
    {
        public BuffApplyRequest ApplyRequest;
        public BuffMO Buff;
        public BuffRuntime Runtime;
        public int TargetActorId;
        public float DurationSeconds;
        public ContinuousTagRequirements Requirements;
        public bool IsExistingRuntime;

        public BuffRuntimeView RuntimeView => new BuffRuntimeView(Runtime);
    }

    internal interface IBuffReadOnlyView
    {
        bool IsValid { get; }
        int BuffId { get; }
        int SourceActorId { get; }
        int StackCount { get; }
        float RemainingSeconds { get; }
        float IntervalRemainingSeconds { get; }
        long SourceContextId { get; }
        long RuntimeContextId { get; }
        long RuntimeContextVersion { get; }
        MobaGameplayOrigin Origin { get; }
        MobaContextSourceView ContextSource { get; }
        BuffContinuousRuntime Continuous { get; }
        MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        MobaSkillRuntimeRetainHandle SkillRuntimeRetainHandle { get; }
    }

    internal interface IBuffLiveViewProvider
    {
        bool TryGetLiveBuffView(out BuffRuntimeView view);
    }

    internal static class BuffLiveViewResolver
    {
        public static bool TryResolve(object payload, out BuffRuntimeView view)
        {
            view = default;
            return payload is IBuffLiveViewProvider provider
                   && provider.TryGetLiveBuffView(out view)
                   && view.IsValid;
        }
    }

    internal interface IBuffMutableState : IBuffReadOnlyView
    {
        void SetSourceActorId(int sourceActorId);
        void SetStackCount(int stackCount);
        void SetRemainingSeconds(float remainingSeconds);
        void SetIntervalRemainingSeconds(float intervalRemainingSeconds);
        void BindSourceContext(long sourceContextId);
        void BindRuntimeContext(long runtimeContextId, long runtimeContextVersion);
        void BindOrigin(in MobaGameplayOrigin origin);
        void BindContextSource(in MobaContextSourceView source);
        void SetContinuous(BuffContinuousRuntime continuous);
        void SetTagRequirements(ContinuousTagRequirements requirements);
        void BindSkillRuntime(in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillRuntimeRetainHandle retainHandle);
        void ClearSkillRuntimeBinding();
        void ClearRuntimeBindings();
    }

    /// <summary>
    /// BuffRuntime 的轻量读写视图，供触发器/效果系统读取活状态，并集中封装运行时字段写入。
    /// </summary>
    internal readonly struct BuffRuntimeView : IBuffMutableState
    {
        private readonly BuffRuntime _runtime;

        public BuffRuntimeView(BuffRuntime runtime)
        {
            _runtime = runtime;
        }

        public bool IsValid => _runtime != null;
        public int BuffId => _runtime != null ? _runtime.BuffId : 0;
        public int SourceActorId => _runtime != null ? _runtime.SourceId : 0;
        public int StackCount => _runtime != null ? _runtime.StackCount : 0;
        public float RemainingSeconds => _runtime != null && _runtime.Continuous != null ? _runtime.Continuous.RemainingSeconds : _runtime != null ? _runtime.Remaining : 0f;
        public float IntervalRemainingSeconds => _runtime != null && _runtime.Continuous != null ? _runtime.Continuous.IntervalRemainingSeconds : _runtime != null ? _runtime.IntervalRemainingSeconds : 0f;
        public long SourceContextId => _runtime != null ? _runtime.SourceContextId : 0L;
        public long RuntimeContextId => _runtime != null ? _runtime.RuntimeContextId : 0L;
        public long RuntimeContextVersion => _runtime != null ? _runtime.RuntimeContextVersion : 0L;
        public MobaGameplayOrigin Origin => _runtime != null ? _runtime.Origin : default;
        public MobaContextSourceView ContextSource => _runtime != null ? _runtime.ContextSource : default;
        public BuffContinuousRuntime Continuous => _runtime != null ? _runtime.Continuous : null;
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle => _runtime != null ? _runtime.SkillRuntimeHandle : default;
        public MobaSkillRuntimeRetainHandle SkillRuntimeRetainHandle => _runtime != null ? _runtime.SkillRuntimeRetainHandle : default;

        public void SetSourceActorId(int sourceActorId)
        {
            if (_runtime == null) return;
            _runtime.SourceId = sourceActorId;
        }

        public void SetStackCount(int stackCount)
        {
            if (_runtime == null) return;
            _runtime.StackCount = stackCount;
        }

        public void SetRemainingSeconds(float remainingSeconds)
        {
            if (_runtime == null) return;
            _runtime.Remaining = remainingSeconds;
        }

        public void SetIntervalRemainingSeconds(float intervalRemainingSeconds)
        {
            if (_runtime == null) return;

            _runtime.IntervalRemainingSeconds = intervalRemainingSeconds;
            if (_runtime.Continuous != null)
            {
                _runtime.Continuous.IntervalRemainingSeconds = intervalRemainingSeconds;
            }
        }

        public void BindSourceContext(long sourceContextId)
        {
            if (_runtime == null) return;
            _runtime.SourceContextId = sourceContextId;
        }

        public void BindRuntimeContext(long runtimeContextId, long runtimeContextVersion)
        {
            if (_runtime == null) return;
            _runtime.RuntimeContextId = runtimeContextId;
            _runtime.RuntimeContextVersion = runtimeContextVersion;
        }

        public void BindOrigin(in MobaGameplayOrigin origin)
        {
            if (_runtime == null) return;
            _runtime.Origin = origin;
        }

        public void BindContextSource(in MobaContextSourceView source)
        {
            if (_runtime == null) return;
            _runtime.ContextSource = source;
        }

        public void SetContinuous(BuffContinuousRuntime continuous)
        {
            if (_runtime == null) return;
            _runtime.Continuous = continuous;
        }

        public void SetTagRequirements(ContinuousTagRequirements requirements)
        {
            if (_runtime == null) return;
            _runtime.TagRequirements = requirements;
        }

        public void BindSkillRuntime(in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (_runtime == null) return;
            _runtime.SkillRuntimeHandle = runtimeHandle;
            _runtime.SkillRuntimeRetainHandle = retainHandle;
        }

        public void ClearSkillRuntimeBinding()
        {
            if (_runtime == null) return;
            _runtime.SkillRuntimeHandle = default;
            _runtime.SkillRuntimeRetainHandle = default;
        }

        public void ClearRuntimeBindings()
        {
            if (_runtime == null) return;
            _runtime.BuffId = 0;
            _runtime.Remaining = 0f;
            _runtime.IntervalRemainingSeconds = 0f;
            _runtime.SourceId = 0;
            _runtime.StackCount = 0;
            _runtime.SourceContextId = 0;
            _runtime.RuntimeContextId = 0;
            _runtime.RuntimeContextVersion = 0;
            _runtime.Origin = default;
            _runtime.ContextSource = default;
            _runtime.Continuous = null;
            _runtime.TagRequirements = null;
            _runtime.ModifierBindings?.Clear();
            _runtime.ModifierBindings = null;
            ClearSkillRuntimeBinding();
        }
    }
}

