using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.GameplayTags;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Runtime {
    /// <summary>
    /// Buff 在持续行为系统中的运行时包装：负责持续时间推进、间隔状态同步和战斗上下文提供。
    /// </summary>
    public sealed class BuffContinuousRuntime : MobaContinuousRuntimeBase, IMobaTickableContinuous, IMobaContinuousIntervalState, IMobaContinuousRuntimeStateSync, IMobaContinuousRuntimeDebugSource, IMobaContinuousExecutionContextProvider
    {
        private readonly BuffContinuousConfig _config;
        // 持续时长的 Q32.32 raw（null=无限期）；与 _config.DurationSeconds(float 视图)同步维护。
        private long? _durationRaw;

        public BuffContinuousRuntime(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, ContinuousTagRequirements tagRequirements)
        {
            if (buff == null) throw new ArgumentNullException(nameof(buff));

            BuffId = buff.Id;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            _config = new BuffContinuousConfig(this, durationSeconds, tagRequirements, buff);
            _durationRaw = durationSeconds > 0f
                ? global::AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(durationSeconds).RawValue
                : (long?)null;
        }

        public int BuffId { get; }
        public int SourceActorId { get; private set; }
        public int TargetActorId { get; }
        public long SourceContextId { get; private set; }
        public int ModifierSourceId { get; private set; }
        public BuffRuntime Runtime { get; private set; }

        public ContinuousTagRequirements TagRequirements => _config.TagRequirements;
        public override IContinuousConfig Config => _config;
        public float RemainingSeconds
        {
            get
            {
                if (!_durationRaw.HasValue) return float.PositiveInfinity;
                var remainingRaw = _durationRaw.Value - ElapsedRaw;
                return remainingRaw > 0L ? Deterministic.Fixed64.FromRaw(remainingRaw).ToSingle() : 0f;
            }
        }

        /// <summary>剩余秒数的 Q32.32 raw（无限期为 null）——回滚快照与位断言用。</summary>
        internal long? RemainingRaw => _durationRaw.HasValue
            ? System.Math.Max(0L, _durationRaw.Value - ElapsedRaw)
            : (long?)null;

        public void BindRuntime(BuffRuntime runtime)
        {
            Runtime = runtime;
        }

        public void BindSourceContext(long sourceContextId)
        {
            SourceContextId = sourceContextId;
            ModifierSourceId = CreateModifierSourceId(sourceContextId, BuffId, TargetActorId);
        }

        /// <summary>
        /// 刷新持续行为投影。叠层、最大层数和标签条件来自 BuffRuntime/配置的最新状态。
        /// </summary>
        public void Refresh(int sourceActorId, float remainingSeconds, int stackCount, int maxStack, ContinuousTagRequirements tagRequirements)
        {
            RefreshRaw(
                sourceActorId,
                remainingSeconds > 0f ? global::AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(remainingSeconds).RawValue : (long?)null,
                stackCount,
                maxStack,
                tagRequirements);
        }

        /// <summary>定点版刷新（回滚恢复路径直用 raw，不经 float 中转）。</summary>
        internal void RefreshRaw(int sourceActorId, long? remainingRaw, int stackCount, int maxStack, ContinuousTagRequirements tagRequirements)
        {
            SourceActorId = sourceActorId;
            _durationRaw = remainingRaw;
            _config.DurationSeconds = remainingRaw.HasValue
                ? Deterministic.Fixed64.FromRaw(remainingRaw.Value).ToSingle()
                : (float?)null;
            _config.Stack = stackCount;
            _config.MaxStack = maxStack > 0 ? maxStack : int.MaxValue;
            _config.TagRequirements = tagRequirements;
            ResetElapsed();
        }

        public void TickManaged(float deltaTimeSeconds)
        {
            if (!IsActive || deltaTimeSeconds <= 0f) return;

            AdvanceElapsed(deltaTimeSeconds);
            if (_durationRaw.HasValue && ElapsedRaw >= _durationRaw.Value)
            {
                End(ContinuousEndReason.Completed);
            }
        }

        /// <summary>
        /// 将持续行为内的剩余时间和间隔时间回写到 BuffRuntime，供表现层和外部查询读取。
        /// </summary>
        public void SyncManagedState()
        {
            if (Runtime == null) return;

            Runtime.IntervalRemainingSeconds = IntervalRemainingSeconds;
            Runtime.Remaining = RemainingSeconds;
        }

        public bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info)
        {
            TryGetContextSource(out var source);
            var handle = Runtime != null ? Runtime.SkillRuntimeHandle : default;
            var sourceContextId = source.SourceContextId != 0 ? source.SourceContextId : SourceContextId;
            info = new MobaContinuousRuntimeDebugInfo(
                "Buff",
                BuffId,
                SourceActorId,
                TargetActorId,
                sourceContextId,
                source.ParentContextId,
                source.RootContextId,
                source.OwnerContextId,
                handle,
                source);
            return BuffId > 0 || SourceActorId > 0 || TargetActorId > 0 || sourceContextId != 0 || handle.IsValid;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
            if (!TryGetContextSource(out var source) || !source.HasExecutionSource)
            {
                return false;
            }

            var combatSource = new MobaCombatContextSource(
                source.ContextKind != EffectContextKind.Unknown ? source.ContextKind : EffectContextKind.Buff,
                source.TraceKind != MobaTraceKind.None ? source.TraceKind : MobaTraceKind.BuffTick,
                source.SourceActorId != 0 ? source.SourceActorId : SourceActorId,
                source.TargetActorId != 0 ? source.TargetActorId : TargetActorId,
                source.SourceContextId != 0 ? source.SourceContextId : SourceContextId,
                source.RootContextId != 0 ? source.RootContextId : source.SourceContextId,
                source.OwnerContextId != 0 ? source.OwnerContextId : source.SourceContextId,
                source.ConfigId != 0 ? source.ConfigId : BuffId,
                source.TriggerId,
                source.Frame,
                source.SkillRuntimeHandle,
                source.RuntimeKind ?? "Buff",
                source.RuntimeConfigId != 0 ? source.RuntimeConfigId : BuffId,
                true);
            context = MobaCombatContextBuilder.FromSource(this, in combatSource);
            return context.HasExecutionSource;
        }

        /// <summary>
        /// 优先使用 BuffRuntime 创建时的来源快照；缺失时根据 source/target/context 构造降级来源。
        /// </summary>
        public bool TryGetContextSource(out MobaContextSourceView source)
        {
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
                    Runtime.ContextSource.TriggerId,
                    Runtime.ContextSource.Frame,
                    "Buff",
                    BuffId,
                    true,
                    Runtime.ContextSource.SkillRuntimeHandle.IsValid ? Runtime.ContextSource.SkillRuntimeHandle : Runtime.SkillRuntimeHandle);
                return source.IsValid;
            }

            var handle = Runtime != null ? Runtime.SkillRuntimeHandle : default;
            var origin = Runtime != null && Runtime.Origin.IsValid
                ? Runtime.Origin
                : new MobaGameplayOrigin(
                    SourceActorId,
                    TargetActorId,
                    MobaTraceKind.BuffApply,
                    BuffId,
                    SourceContextId,
                    SourceContextId,
                    SourceContextId,
                    SourceContextId,
                    handle);
            var lineageContext = origin.ToLineageContext(EffectContextKind.Buff);
            source = MobaContextSourceView.FromLineage(
                in lineageContext,
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime,
                handle.IsValid ? handle : origin.SkillRuntimeHandle,
                Runtime != null,
                "Buff",
                BuffId);
            return source.IsValid;
        }

        private static int CreateModifierSourceId(long sourceContextId, int buffId, int targetActorId)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + buffId;
                hash = hash * 31 + targetActorId;
                return hash == 0 ? buffId : hash;
            }
        }

        private sealed class BuffContinuousConfig : MobaContinuousConfigBase, IMobaContinuousOwnerBoundTriggerConfig
        {
            private readonly BuffContinuousRuntime _runtime;

            private readonly BuffMO _buff;

            public BuffContinuousConfig(BuffContinuousRuntime runtime, float durationSeconds, ContinuousTagRequirements tagRequirements, BuffMO buff)
                : base(durationSeconds, tagRequirements, buff?.Modifiers)
            {
                _runtime = runtime;
                _buff = buff;
            }

            public override string Id => $"buff:{_runtime.TargetActorId}:{_runtime.BuffId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.TargetActorId;
            public override int OwnerActorId => _runtime.TargetActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public override GameplayTagSource TagSource => CreateSource(_runtime);
            public override float IntervalSeconds => _buff != null && _buff.IntervalMs > 0 ? _buff.IntervalMs / 1000f : 0f;
            public override IReadOnlyList<int> IntervalEffectIds => _buff?.OnIntervalEffects ?? Array.Empty<int>();
            public IReadOnlyList<int> TriggerIds => _buff?.TriggerIds ?? Array.Empty<int>();

            private static GameplayTagSource CreateSource(BuffContinuousRuntime runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.SourceActorId != 0) return new GameplayTagSource(runtime.SourceActorId);
                return GameplayTagSource.System;
            }
        }
    }

    public sealed class BuffModifierBinding
    {
        public int AttributeType;
        public int Handle;
        public int SourceId;
    }
}

