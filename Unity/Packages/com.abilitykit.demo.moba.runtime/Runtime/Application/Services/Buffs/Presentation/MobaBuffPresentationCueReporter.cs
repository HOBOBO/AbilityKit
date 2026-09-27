using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba.StateSync;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs.Presentation {
    /// <summary>
    /// Buff 表现提示上报器：把 Buff 生命周期阶段转换成表现层可消费的快照 cue。
    /// </summary>
    internal sealed class MobaBuffPresentationCueReporter
    {
        private const string OwnerKindBuff = "Buff";
        private const int NumericParamRadius = 2;
 
        private readonly MobaConfigDatabase _configs;
        private readonly MobaPresentationCueSnapshotService _snapshots;
        private readonly Dictionary<int, int[]> _singleTargetArrays = new Dictionary<int, int[]>(32);
        private readonly Dictionary<BuffPresentationInstanceKey, string> _instanceKeys = new Dictionary<BuffPresentationInstanceKey, string>(64);

        public MobaBuffPresentationCueReporter(MobaConfigDatabase configs, MobaPresentationCueSnapshotService snapshots)
        {
            _configs = configs;
            _snapshots = snapshots;
        }

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
        private void Report(MobaPresentationCueStage stage, BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, MobaExecutionEndReason reason)
        {
            if (_snapshots == null) return;
            if (buff == null) return;
            if (runtime == null) return;
            if (buff.PresentationTemplateId <= 0) return;
            if (!TryResolveTemplate(buff.PresentationTemplateId, out var template) || template == null) return;

            var entry = BuildEntry(stage, buff, template, sourceActorId, targetActorId, runtime, reason);
            _snapshots.Report(in entry);
        }

        private MobaPresentationCueSnapshotEntry BuildEntry(
            MobaPresentationCueStage stage,
            BuffMO buff,
            PresentationTemplateMO template,
            int sourceActorId,
            int targetActorId,
            BuffRuntime runtime,
            MobaExecutionEndReason reason)
        {
            var remainingSeconds = ResolveRemainingSeconds(runtime);
            var instanceKey = GetInstanceKey(buff.Id, targetActorId, runtime.SourceContextId);
            var numericParamKeys = template.Radius > 0f ? new[] { NumericParamRadius } : null;
            var numericParamValues = template.Radius > 0f ? new[] { template.Radius } : null;

            return new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)stage,
                CueKind = OwnerKindBuff,
                TemplateId = template.Id,
                VfxId = template.AssetId,
                RequestKey = instanceKey,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                Targets = GetSingleTargetArray(targetActorId),
                OffsetX = template.OffsetX,
                OffsetY = template.OffsetY,
                OffsetZ = template.OffsetZ,
                DurationMsOverride = template.DefaultDurationMs > 0 ? template.DefaultDurationMs : buff.DurationMs,
                Scale = template.Scale > 0f ? template.Scale : 1f,
                ColorR = template.ColorR != 0f ? template.ColorR : 1f,
                ColorG = template.ColorG != 0f ? template.ColorG : 1f,
                ColorB = template.ColorB != 0f ? template.ColorB : 1f,
                ColorA = template.ColorA != 0f ? template.ColorA : 1f,
                OwnerKind = OwnerKindBuff,
                InstanceId = runtime.SourceContextId,
                InstanceKey = instanceKey,
                StackCount = runtime.StackCount,
                MaxStackCount = buff.MaxStacks,
                ElapsedSeconds = runtime.Continuous != null ? runtime.Continuous.ElapsedSeconds : 0f,
                RemainingSeconds = remainingSeconds,
                LifecycleReason = (int)reason,
                NumericParamKeys = numericParamKeys,
                NumericParamValues = numericParamValues
            };
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

        /// <summary>
        /// 为同一 Buff 实例生成稳定表现 key，确保刷新、叠层和结束 cue 能命中同一个表现对象。
        /// </summary>
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

