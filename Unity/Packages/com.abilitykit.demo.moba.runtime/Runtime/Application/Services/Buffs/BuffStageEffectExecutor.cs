using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs {
    /// <summary>
    /// Buff 阶段效果执行器：把 add/remove/interval 等阶段配置的 triggerId 转换成效果执行请求。
    /// </summary>
    internal sealed class BuffStageEffectExecutor
    {
        private readonly MobaTriggerExecutionGateway _triggers;

        public BuffStageEffectExecutor(MobaTriggerExecutionGateway triggers)
        {
            _triggers = triggers;
        }

        /// <summary>
        /// 执行某个 Buff 阶段下的全部触发器。执行前会冻结来源快照，保证移除后仍能正确溯源。
        /// </summary>
        public void Execute(IReadOnlyList<int> triggerIds, int buffId, int sourceActorId, int targetActorId, long sourceContextId, string stage, BuffRuntime runtime, TraceLifecycleReason removeReason = TraceLifecycleReason.None, float durationSeconds = 0f)
        {
            if (_triggers == null) return;
            if (triggerIds == null || triggerIds.Count == 0) return;

            var persistentSource = CapturePersistentSource(buffId, sourceActorId, targetActorId, sourceContextId, stage, runtime);
            if (!ValidateSource(buffId, sourceActorId, targetActorId, sourceContextId, stage, runtime, in persistentSource)) return;

            for (int i = 0; i < triggerIds.Count; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;

                var payload = CreatePayload(triggerId, buffId, sourceActorId, targetActorId, sourceContextId, stage, runtime, in persistentSource, removeReason, durationSeconds);
                var request = MobaTriggerExecutionRequest<BuffTriggerContext>.Create(triggerId, payload, "buff.stage." + stage);
                _triggers.ExecuteDirectTrigger(in request);
            }
        }

        private static bool ValidateSource(int buffId, int sourceActorId, int targetActorId, long sourceContextId, string stage, BuffRuntime runtime, in MobaPersistentContextSourceSnapshot persistentSource)
        {
            if (runtime == null && !persistentSource.HasExecutionSource)
            {
                AbilityKit.Core.Logging.Log.Warning($"[BuffStageEffectExecutor] missing runtime or persistent source. buffId={buffId} stage={stage} sourceActorId={sourceActorId} targetActorId={targetActorId} sourceContextId={sourceContextId}");
                return false;
            }

            if (sourceActorId <= 0 || targetActorId <= 0 || sourceContextId == 0L)
            {
                AbilityKit.Core.Logging.Log.Warning($"[BuffStageEffectExecutor] incomplete source context. buffId={buffId} stage={stage} sourceActorId={sourceActorId} targetActorId={targetActorId} sourceContextId={sourceContextId}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 捕获可持久化来源。移除阶段 runtime 可能马上被清理，因此效果 payload 不能只依赖活对象引用。
        /// </summary>
        private static MobaPersistentContextSourceSnapshot CapturePersistentSource(int buffId, int sourceActorId, int targetActorId, long sourceContextId, string stage, BuffRuntime runtime)
        {
            var traceKind = BuffTriggerContext.ResolveTraceKind(stage);
            if (runtime != null && runtime.ContextSource.IsValid)
            {
                var source = new MobaContextSourceView(
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    runtime.ContextSource.ContextKind != EffectContextKind.Unknown ? runtime.ContextSource.ContextKind : EffectContextKind.Buff,
                    traceKind,
                    runtime.ContextSource.SourceActorId != 0 ? runtime.ContextSource.SourceActorId : sourceActorId,
                    runtime.ContextSource.TargetActorId != 0 ? runtime.ContextSource.TargetActorId : targetActorId,
                    runtime.ContextSource.SourceContextId != 0 ? runtime.ContextSource.SourceContextId : sourceContextId,
                    runtime.ContextSource.ParentContextId != 0 ? runtime.ContextSource.ParentContextId : sourceContextId,
                    runtime.ContextSource.RootContextId != 0 ? runtime.ContextSource.RootContextId : sourceContextId,
                    runtime.ContextSource.OwnerContextId != 0 ? runtime.ContextSource.OwnerContextId : sourceContextId,
                    runtime.ContextSource.ConfigId != 0 ? runtime.ContextSource.ConfigId : buffId,
                    0,
                    runtime.ContextSource.Frame,
                    MobaRuntimeKindNames.Buff,
                    buffId,
                    false,
                    runtime.ContextSource.SkillRuntimeHandle.IsValid ? runtime.ContextSource.SkillRuntimeHandle : runtime.SkillRuntimeHandle);
                return MobaPersistentContextSourceSnapshot.FromContextSource(in source);
            }

            var fallback = new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Snapshot,
                EffectContextKind.Buff,
                traceKind,
                sourceActorId,
                targetActorId,
                sourceContextId,
                sourceContextId,
                sourceContextId,
                sourceContextId,
                buffId,
                0,
                0,
                MobaRuntimeKindNames.Buff,
                buffId,
                false,
                runtime != null ? runtime.SkillRuntimeHandle : default);
            return MobaPersistentContextSourceSnapshot.FromContextSource(in fallback);
        }

        private static BuffTriggerContext CreatePayload(int triggerId, int buffId, int sourceActorId, int targetActorId, long sourceContextId, string stage, BuffRuntime runtime, in MobaPersistentContextSourceSnapshot persistentSource, TraceLifecycleReason removeReason, float durationSeconds)
        {
            return new BuffTriggerContext
            {
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceContextId = sourceContextId,
                TriggerId = triggerId,
                BuffId = buffId,
                Stage = stage,
                RuntimeContextId = runtime != null ? runtime.RuntimeContextId : 0L,
                RuntimeContextVersion = runtime != null ? runtime.RuntimeContextVersion : 0L,
                RuntimeContextIdentity = runtime != null ? runtime.RuntimeContextIdentity : default,
                StackCountSnapshot = runtime != null ? runtime.StackCount : 0,
                RemainingSecondsSnapshot = runtime != null ? runtime.Remaining : 0f,
                DurationSecondsSnapshot = durationSeconds,
                RemoveReason = removeReason,
                Runtime = runtime,
                PersistentSource = persistentSource,
            };
        }
    }

    internal interface IBuffTriggerContext : IMobaTriggerInvocationContext, IMobaActorContextProvider, IBuffLiveViewProvider, IMobaRuntimeContextPayload
    {
        int BuffId { get; }
        string Stage { get; }
        long RuntimeContextId { get; }
        long RuntimeContextVersion { get; }
        int StackCountSnapshot { get; }
        float RemainingSecondsSnapshot { get; }
        float DurationSecondsSnapshot { get; }
        int StackCount { get; }
        float DurationSeconds { get; }
        TraceLifecycleReason RemoveReason { get; }
        bool TryGetBuffRuntime(out BuffRuntime runtime);
    }

    /// <summary>
    /// Buff 触发器上下文：同时提供 Actor、trace、runtime、技能运行时和持久来源视图。
    /// </summary>
    internal sealed class BuffTriggerContext : MobaTriggerInvocationContextBase, IBuffTriggerContext, IMobaTriggerRuntimeContext<BuffRuntime>, IMobaTriggerSkillRuntimeContext, IMobaTriggerStageSnapshotProvider, IMobaContextSourceProvider, IMobaPersistentContextSourceProvider
    {
        public override EffectContextKind Kind => EffectContextKind.Buff;
        public int BuffId { get; set; }
        public string Stage { get; set; }
        public long RuntimeContextId { get; set; }
        public long RuntimeContextVersion { get; set; }
        public AbilityKit.Context.ContextEntityReference RuntimeContextIdentity { get; set; }
        public int StackCountSnapshot { get; set; }
        public float RemainingSecondsSnapshot { get; set; }
        public float DurationSecondsSnapshot { get; set; }
        public int StackCount
        {
            get => StackCountSnapshot;
            set => StackCountSnapshot = value;
        }
        public float DurationSeconds
        {
            get => DurationSecondsSnapshot;
            set => DurationSecondsSnapshot = value;
        }
        public TraceLifecycleReason RemoveReason { get; set; }
        public BuffRuntime Runtime { get; set; }
        public MobaPersistentContextSourceSnapshot PersistentSource { get; set; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle => Runtime != null && Runtime.SkillRuntimeHandle.IsValid ? Runtime.SkillRuntimeHandle : PersistentSource.Source.SkillRuntimeHandle;
        public override MobaTriggerLineageContext LineageContext => ResolveLineageContext();
        public override MobaTriggerTraceContext TraceContext => LineageContext.ToTraceContext();
        public override MobaGameplayOrigin Origin
        {
            get
            {
                if (Runtime != null && Runtime.Origin.IsValid) return Runtime.Origin;

                var lineageContext = LineageContext;
                var handle = SkillRuntimeHandle;
                return MobaGameplayOrigin.FromLineageContext(in lineageContext, in handle);
            }
        }

        public override bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = LineageContext;
            return true;
        }

        public override bool TryGetTraceContext(out MobaTriggerTraceContext traceContext)
        {
            traceContext = TraceContext;
            return true;
        }

        public bool TryGetRuntime(out BuffRuntime runtime)
        {
            runtime = Runtime;
            return runtime != null;
        }

        public bool TryGetBuffRuntime(out BuffRuntime runtime) => TryGetRuntime(out runtime);

        public bool TryGetRuntimeContext(out MobaRuntimeContextReference reference)
        {
            reference = MobaRuntimeContextReference.FromIdentityOrLegacy(RuntimeContextId, RuntimeContextVersion, RuntimeContextIdentity);
            return reference.IsValid;
        }
 
        public bool TryGetLiveBuffView(out BuffRuntimeView view)
        {
            view = new BuffRuntimeView(Runtime);
            return view.IsValid;
        }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public override bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = Origin;
            return origin.IsValid;
        }

        public bool TryGetSkillRuntimeHandle(out MobaSkillCastRuntimeHandle handle)
        {
            handle = SkillRuntimeHandle;
            return handle.IsValid;
        }

        public bool TryGetStageSnapshot(out MobaTriggerStageSnapshot snapshot)
        {
            snapshot = new MobaTriggerStageSnapshot(
                StackCountSnapshot,
                0f,
                RemainingSecondsSnapshot,
                DurationSecondsSnapshot);
            return snapshot.IsValid;
        }

        public bool TryGetPersistentContextSource(out MobaPersistentContextSourceSnapshot snapshot)
        {
            if (PersistentSource.IsValid)
            {
                snapshot = PersistentSource;
                return snapshot.HasExecutionSource;
            }

            if (TryGetContextSource(out var source))
            {
                snapshot = MobaPersistentContextSourceSnapshot.FromContextSource(in source);
                return snapshot.HasExecutionSource;
            }

            snapshot = default;
            return false;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (PersistentSource.IsValid && (Runtime == null || !Runtime.ContextSource.IsValid))
            {
                return PersistentSource.TryGetContextSource(out source);
            }

            if (Runtime != null && Runtime.ContextSource.IsValid)
            {
                source = new MobaContextSourceView(
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.LiveRuntime,
                    Runtime.ContextSource.ContextKind != EffectContextKind.Unknown ? Runtime.ContextSource.ContextKind : EffectContextKind.Buff,
                    Runtime.ContextSource.TraceKind,
                    Runtime.ContextSource.SourceActorId != 0 ? Runtime.ContextSource.SourceActorId : SourceActorId,
                    Runtime.ContextSource.TargetActorId != 0 ? Runtime.ContextSource.TargetActorId : TargetActorId,
                    Runtime.ContextSource.SourceContextId != 0 ? Runtime.ContextSource.SourceContextId : SourceContextId,
                    Runtime.ContextSource.ParentContextId,
                    Runtime.ContextSource.RootContextId,
                    Runtime.ContextSource.OwnerContextId,
                    Runtime.ContextSource.ConfigId != 0 ? Runtime.ContextSource.ConfigId : BuffId,
                    TriggerId,
                    Runtime.ContextSource.Frame,
                    "Buff",
                    BuffId,
                    true,
                    Runtime.ContextSource.SkillRuntimeHandle.IsValid ? Runtime.ContextSource.SkillRuntimeHandle : SkillRuntimeHandle);
                return source.IsValid;
            }

            var lineageContext = LineageContext;
            source = MobaContextSourceView.FromLineage(
                in lineageContext,
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Snapshot,
                SkillRuntimeHandle,
                false,
                "Buff",
                BuffId);
            return source.IsValid;
        }

        private MobaTriggerLineageContext ResolveLineageContext()
        {
            if (Runtime != null && Runtime.Origin.IsValid)
            {
                var origin = Runtime.Origin;
                return new MobaTriggerLineageContext(
                    Kind,
                    ResolveTraceKind(Stage),
                    origin.SourceActorId != 0 ? origin.SourceActorId : SourceActorId,
                    origin.TargetActorId != 0 ? origin.TargetActorId : TargetActorId,
                    origin.EffectiveParentContextId != 0 ? origin.EffectiveParentContextId : SourceContextId,
                    origin.EffectiveRootContextId != 0 ? origin.EffectiveRootContextId : SourceContextId,
                    origin.OwnerContextId,
                    BuffId);
            }

            return new MobaTriggerLineageContext(Kind, ResolveTraceKind(Stage), SourceActorId, TargetActorId, SourceContextId, SourceContextId, SourceContextId, BuffId);
        }

        internal static MobaTraceKind ResolveTraceKind(string stage)
        {
            if (MobaBuffTriggering.Stages.IsRemove(stage)) return MobaTraceKind.BuffRemove;
            if (MobaBuffTriggering.Stages.IsInterval(stage)) return MobaTraceKind.BuffTick;
            return MobaTraceKind.BuffApply;
        }
    }
}

