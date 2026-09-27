using System;
using AbilityKit.Protocol.Moba.StateSync;
using SharePresentationCueStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Maps MOBA wire-protocol presentation cues into platform-neutral presentation contracts.
    /// </summary>
    public static class PresentationCueSnapshotMapper
    {
        public static PresentationCueData[] Map(MobaPresentationCueSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<PresentationCueData>();
            }

            var cues = new PresentationCueData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                cues[i] = Map(in entries[i]);
            }

            return cues;
        }

        public static PresentationCueData Map(in MobaPresentationCueSnapshotEntry entry)
        {
            return new PresentationCueData(
                stage: (SharePresentationCueStage)entry.Stage,
                cueKind: entry.CueKind,
                cueVfxId: entry.CueVfxId,
                cueSfxId: entry.CueSfxId,
                templateId: entry.TemplateId,
                vfxId: entry.VfxId,
                sfxId: entry.SfxId,
                requestKey: entry.RequestKey,
                sourceActorId: entry.SourceActorId,
                targetActorId: entry.TargetActorId,
                triggerEventId: entry.TriggerEventId,
                triggerEventName: entry.TriggerEventName,
                triggerId: entry.TriggerId,
                phase: entry.Phase,
                priority: entry.Priority,
                order: entry.Order,
                actionIndex: entry.ActionIndex,
                interruptReason: entry.InterruptReason,
                interruptSourceName: entry.InterruptSourceName,
                interruptTriggerId: entry.InterruptTriggerId,
                interruptConditionPassed: entry.InterruptConditionPassed,
                targets: entry.Targets ?? Array.Empty<int>(),
                positions: DecodePositions(entry.Positions),
                offsetX: entry.OffsetX,
                offsetY: entry.OffsetY,
                offsetZ: entry.OffsetZ,
                durationMsOverride: entry.DurationMsOverride,
                scale: entry.Scale,
                colorR: entry.ColorR,
                colorG: entry.ColorG,
                colorB: entry.ColorB,
                colorA: entry.ColorA,
                ownerKind: entry.OwnerKind,
                instanceId: entry.InstanceId,
                instanceKey: entry.InstanceKey,
                stackCount: entry.StackCount,
                maxStackCount: entry.MaxStackCount,
                elapsedSeconds: entry.ElapsedSeconds,
                remainingSeconds: entry.RemainingSeconds,
                lifecycleReason: entry.LifecycleReason,
                contextKind: entry.ContextKind,
                originKind: entry.OriginKind,
                sourceContextId: entry.SourceContextId,
                rootContextId: entry.RootContextId,
                ownerContextId: entry.OwnerContextId,
                sourceConfigId: entry.SourceConfigId,
                contextEventId: entry.ContextEventId,
                numericParamKeys: entry.NumericParamKeys ?? Array.Empty<int>(),
                numericParamValues: entry.NumericParamValues ?? Array.Empty<float>(),
                stringParamKeys: entry.StringParamKeys ?? Array.Empty<string>(),
                stringParamValues: entry.StringParamValues ?? Array.Empty<string>(),
                predictionKey: entry.PredictionKey,
                predictionState: (PresentationCuePredictionState)entry.PredictionState,
                predictedFrame: entry.PredictedFrame,
                confirmedFrame: entry.ConfirmedFrame);
        }

        private static SnapshotVec3[] DecodePositions(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Array.Empty<SnapshotVec3>();
            }

            var count = values.Length / 3;
            var positions = new SnapshotVec3[count];
            for (int i = 0; i < count; i++)
            {
                var offset = i * 3;
                positions[i] = new SnapshotVec3(values[offset], values[offset + 1], values[offset + 2]);
            }

            return positions;
        }
    }
}
