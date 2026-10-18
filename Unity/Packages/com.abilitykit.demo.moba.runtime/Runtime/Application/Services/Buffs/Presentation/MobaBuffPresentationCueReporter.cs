using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services.Buffs.Presentation {
    /// <summary>
    /// Buff 表现提示上报器：把 Buff 生命周期阶段转换成表现层可消费的快照 cue。
    ///
    /// 生命周期方法就是"要支持哪些表现需求"的完整声明——新增一种表现只需在这里加一行，
    /// 字段装配、key 生成、legacy 回填都由基类负责。
    /// </summary>
    internal sealed class MobaBuffPresentationCueReporter : MobaCueReporterBase
    {
        private readonly MobaConfigDatabase _configs;
        private readonly Dictionary<int, int[]> _singleTargetArrays = new Dictionary<int, int[]>(32);
        private readonly Dictionary<BuffPresentationInstanceKey, string> _instanceKeys = new Dictionary<BuffPresentationInstanceKey, string>(64);

        // 当前上报的 buff，由生命周期方法设置后再交给 Populate。
        private BuffMO _buff;
        private int _sourceActorId;
        private int _targetActorId;
        private BuffRuntime _runtime;
        private MobaExecutionEndReason _reason;
        private PresentationTemplateMO _template;

        public MobaBuffPresentationCueReporter(MobaConfigDatabase configs, MobaPresentationCueSnapshotService snapshots)
            : base(snapshots)
        {
            _configs = configs;
        }

        protected override PresentationCueDomain Domain => PresentationCueDomain.Buff;

        public void Started(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            Report(MobaPresentationCueStage.Started, buff, sourceActorId, targetActorId, runtime, MobaExecutionEndReason.None);
        }

        public void Refreshed(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            Report(MobaPresentationCueStage.Refreshed, buff, sourceActorId, targetActorId, runtime, MobaExecutionEndReason.None);
        }

        public void StackChanged(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            Report(MobaPresentationCueStage.StackChanged, buff, sourceActorId, targetActorId, runtime, MobaExecutionEndReason.None);
        }

        public void Ticked(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            Report(MobaPresentationCueStage.Ticked, buff, sourceActorId, targetActorId, runtime, MobaExecutionEndReason.None);
        }

        public void Ended(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, MobaExecutionEndReason reason)
        {
            Report(ToEndStage(reason), buff, sourceActorId, targetActorId, runtime, reason);
        }

        /// <summary>
        /// 统一上报入口。没有表现模板的 Buff 会静默跳过，逻辑层不依赖表现配置。
        /// </summary>
        private void Report(
            MobaPresentationCueStage stage,
            BuffMO buff,
            int sourceActorId,
            int targetActorId,
            BuffRuntime runtime,
            MobaExecutionEndReason reason)
        {
            if (buff == null) return;
            if (runtime == null) return;
            if (buff.PresentationTemplateId <= 0) return;
            if (!TryResolveTemplate(buff.PresentationTemplateId, out var template) || template == null) return;

            _buff = buff;
            _sourceActorId = sourceActorId;
            _targetActorId = targetActorId;
            _runtime = runtime;
            _reason = reason;
            _template = template;

            var draft = CueDraft.Create(PresentationCueDomain.Buff, stage);
            draft.Class = PresentationCueClass.Lifecycle | PresentationCueClass.Placement;
            Emit(ref draft);
        }

        protected override void Populate(ref CueDraft draft)
        {
            draft.ApplyTemplate(_template);
            draft.SourceActorId = _sourceActorId;
            draft.TargetActorId = _targetActorId;
            draft.Targets = GetSingleTargetArray(_targetActorId);
            if (draft.DurationMsOverride <= 0) draft.DurationMsOverride = _buff.DurationMs;
            draft.InstanceId = _runtime.SourceContextId;
            var source = _runtime.ContextSource;
            var origin = _runtime.Origin;
            draft.ContextKind = (int)(source.ContextKind != EffectContextKind.Unknown ? source.ContextKind : EffectContextKind.Buff);
            draft.OriginKind = (int)(source.ExecutionKind != MobaExecutionKind.None ? source.ExecutionKind : origin.ImmediateKind);
            draft.SourceContextId = source.SourceContextId != 0 ? source.SourceContextId : _runtime.SourceContextId;
            draft.RootContextId = source.RootContextId != 0 ? source.RootContextId : origin.EffectiveRootContextId;
            draft.OwnerContextId = source.OwnerContextId != 0 ? source.OwnerContextId : origin.OwnerContextId;
            draft.SourceConfigId = source.ConfigId != 0 ? source.ConfigId : origin.ImmediateConfigId;
            draft.ContextEventId = $"buff:{_buff.Id}:{_runtime.SourceContextId}";
            draft.StackCount = _runtime.StackCount;
            draft.MaxStackCount = _buff.MaxStacks;
            draft.ElapsedSeconds = _runtime.Continuous != null ? _runtime.Continuous.ElapsedSeconds : 0f;
            draft.RemainingSeconds = ResolveRemainingSeconds(_runtime);
            draft.LifecycleReason = (int)_reason;
        }

        /// <summary>
        /// 为同一 Buff 实例生成稳定表现 key，确保刷新、叠层和结束 cue 能命中同一个表现对象。
        /// </summary>
        protected override string ResolveInstanceKey(in CueDraft draft)
        {
            return GetInstanceKey(_buff.Id, _targetActorId, _runtime.SourceContextId);
        }

        private bool TryResolveTemplate(int templateId, out PresentationTemplateMO template)
        {
            template = null;
            if (_configs == null) return false;
            return _configs.GetTable<PresentationTemplateMO>().TryGet(templateId, out template);
        }

        private static float ResolveRemainingSeconds(BuffRuntime runtime)
        {
            if (runtime == null) return 0f;
            if (runtime.Continuous != null) return runtime.Continuous.RemainingSeconds;
            return runtime.Remaining;
        }

        private static MobaPresentationCueStage ToEndStage(MobaExecutionEndReason reason)
        {
            switch (reason)
            {
                case MobaExecutionEndReason.Expired:
                    return MobaPresentationCueStage.Expired;
                case MobaExecutionEndReason.Completed:
                    return MobaPresentationCueStage.Completed;
                default:
                    return MobaPresentationCueStage.Removed;
            }
        }

        private int[] GetSingleTargetArray(int targetActorId)
        {
            if (targetActorId <= 0) return null;
            if (_singleTargetArrays.TryGetValue(targetActorId, out var targets) && targets != null) return targets;

            targets = new[] { targetActorId };
            _singleTargetArrays[targetActorId] = targets;
            return targets;
        }

        private string GetInstanceKey(int buffId, int targetActorId, long sourceContextId)
        {
            var key = new BuffPresentationInstanceKey(buffId, targetActorId, sourceContextId);
            if (_instanceKeys.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)) return value;

            value = $"buff:{targetActorId}:{buffId}:{sourceContextId}";
            _instanceKeys[key] = value;
            return value;
        }

        private readonly struct BuffPresentationInstanceKey : IEquatable<BuffPresentationInstanceKey>
        {
            private readonly int _buffId;
            private readonly int _targetActorId;
            private readonly long _sourceContextId;

            public BuffPresentationInstanceKey(int buffId, int targetActorId, long sourceContextId)
            {
                _buffId = buffId;
                _targetActorId = targetActorId;
                _sourceContextId = sourceContextId;
            }

            public bool Equals(BuffPresentationInstanceKey other)
            {
                return _buffId == other._buffId
                       && _targetActorId == other._targetActorId
                       && _sourceContextId == other._sourceContextId;
            }

            public override bool Equals(object obj)
            {
                return obj is BuffPresentationInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _buffId;
                    hash = (hash * 397) ^ _targetActorId;
                    hash = (hash * 397) ^ _sourceContextId.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
