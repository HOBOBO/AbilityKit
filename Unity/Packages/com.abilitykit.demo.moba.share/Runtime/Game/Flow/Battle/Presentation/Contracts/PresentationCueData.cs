using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral presentation cue snapshot contract.
    /// </summary>
    public readonly struct PresentationCueData
    {
        public PresentationCueStage Stage { get; }
        public string CueKind { get; }
        public string CueVfxId { get; }
        public string CueSfxId { get; }
        public int TemplateId { get; }
        public int VfxId { get; }
        public int SfxId { get; }
        public string RequestKey { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int TriggerEventId { get; }
        public string TriggerEventName { get; }
        public int TriggerId { get; }
        public int Phase { get; }
        public int Priority { get; }
        public int Order { get; }
        public int ActionIndex { get; }
        public int InterruptReason { get; }
        public string InterruptSourceName { get; }
        public int InterruptTriggerId { get; }
        public bool InterruptConditionPassed { get; }
        public IReadOnlyList<int> Targets { get; }
        public IReadOnlyList<SnapshotVec3> Positions { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }
        public int DurationMsOverride { get; }
        public float Scale { get; }
        public float ColorR { get; }
        public float ColorG { get; }
        public float ColorB { get; }
        public float ColorA { get; }
        public string OwnerKind { get; }
        public long InstanceId { get; }
        public string InstanceKey { get; }
        public int StackCount { get; }
        public int MaxStackCount { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public int LifecycleReason { get; }
        public int ContextKind { get; }
        public int OriginKind { get; }
        public long SourceContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int SourceConfigId { get; }
        public string ContextEventId { get; }
        public IReadOnlyList<int> NumericParamKeys { get; }
        public IReadOnlyList<float> NumericParamValues { get; }
        public IReadOnlyList<string> StringParamKeys { get; }
        public IReadOnlyList<string> StringParamValues { get; }
        public int PredictionKey { get; }
        public PresentationCuePredictionState PredictionState { get; }
        public int PredictedFrame { get; }
        public int ConfirmedFrame { get; }

        public PresentationCueData(
            PresentationCueStage stage,
            string cueKind,
            string cueVfxId,
            string cueSfxId,
            int templateId,
            int vfxId,
            int sfxId,
            string requestKey,
            int sourceActorId,
            int targetActorId,
            int triggerEventId,
            string triggerEventName,
            int triggerId,
            int phase,
            int priority,
            int order,
            int actionIndex,
            int interruptReason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            IReadOnlyList<int> targets,
            IReadOnlyList<SnapshotVec3> positions,
            float offsetX,
            float offsetY,
            float offsetZ,
            int durationMsOverride,
            float scale,
            float colorR,
            float colorG,
            float colorB,
            float colorA,
            string ownerKind = null,
            long instanceId = 0,
            string instanceKey = null,
            int stackCount = 0,
            int maxStackCount = 0,
            float elapsedSeconds = 0f,
            float remainingSeconds = 0f,
            int lifecycleReason = 0,
            int contextKind = 0,
            int originKind = 0,
            long sourceContextId = 0,
            long rootContextId = 0,
            long ownerContextId = 0,
            int sourceConfigId = 0,
            string contextEventId = null,
            IReadOnlyList<int> numericParamKeys = null,
            IReadOnlyList<float> numericParamValues = null,
            IReadOnlyList<string> stringParamKeys = null,
            IReadOnlyList<string> stringParamValues = null,
            int predictionKey = 0,
            PresentationCuePredictionState predictionState = PresentationCuePredictionState.None,
            int predictedFrame = 0,
            int confirmedFrame = 0)
        {
            Stage = stage;
            CueKind = cueKind;
            CueVfxId = cueVfxId;
            CueSfxId = cueSfxId;
            TemplateId = templateId;
            VfxId = vfxId;
            SfxId = sfxId;
            RequestKey = requestKey;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            TriggerEventId = triggerEventId;
            TriggerEventName = triggerEventName;
            TriggerId = triggerId;
            Phase = phase;
            Priority = priority;
            Order = order;
            ActionIndex = actionIndex;
            InterruptReason = interruptReason;
            InterruptSourceName = interruptSourceName;
            InterruptTriggerId = interruptTriggerId;
            InterruptConditionPassed = interruptConditionPassed;
            Targets = targets ?? Array.Empty<int>();
            Positions = positions ?? Array.Empty<SnapshotVec3>();
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
            DurationMsOverride = durationMsOverride;
            Scale = scale;
            ColorR = colorR;
            ColorG = colorG;
            ColorB = colorB;
            ColorA = colorA;
            OwnerKind = ownerKind;
            InstanceId = instanceId;
            InstanceKey = instanceKey;
            StackCount = stackCount;
            MaxStackCount = maxStackCount;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            LifecycleReason = lifecycleReason;
            ContextKind = contextKind;
            OriginKind = originKind;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SourceConfigId = sourceConfigId;
            ContextEventId = contextEventId;
            NumericParamKeys = numericParamKeys ?? Array.Empty<int>();
            NumericParamValues = numericParamValues ?? Array.Empty<float>();
            StringParamKeys = stringParamKeys ?? Array.Empty<string>();
            StringParamValues = stringParamValues ?? Array.Empty<string>();
            PredictionKey = predictionKey;
            PredictionState = predictionState;
            PredictedFrame = predictedFrame;
            ConfirmedFrame = confirmedFrame;
        }
    }

    public enum PresentationCueStage
    {
        None = 0,
        ConditionPassed = 1,
        ConditionFailed = 2,
        BeforeAction = 3,
        Executed = 4,
        Interrupted = 5,
        Skipped = 6,
        Started = 20,
        Ticked = 21,
        Refreshed = 22,
        StackChanged = 23,
        Expired = 24,
        Removed = 25,
        Completed = 26,
    }

    public enum PresentationCuePredictionState
    {
        None = 0,
        Predicted = 1,
        ServerConfirmed = 2,
        Corrected = 3,
        Rejected = 4,
    }

    public readonly struct SnapshotVec3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public SnapshotVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
