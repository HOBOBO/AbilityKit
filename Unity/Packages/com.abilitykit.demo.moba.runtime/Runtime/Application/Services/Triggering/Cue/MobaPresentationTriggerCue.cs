using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class MobaPresentationTriggerCue : ITriggerCue
    {
        private const int NumericParamScale = 1;
        private const int NumericParamRadius = 2;
        private const string StringParamColor = "color";

        private readonly MobaPresentationCueSnapshotService _snapshots;
        private readonly IMobaPresentationCueResolver _resolver;
        private readonly TriggerCueDescriptor _descriptor;
        private readonly MobaPresentationCueDefinition _definition;

        public MobaPresentationTriggerCue(MobaPresentationCueSnapshotService snapshots, in TriggerCueDescriptor descriptor)
            : this(snapshots, null, in descriptor)
        {
        }

        public MobaPresentationTriggerCue(MobaPresentationCueSnapshotService snapshots, IMobaPresentationCueResolver resolver, in TriggerCueDescriptor descriptor)
        {
            _snapshots = snapshots;
            _resolver = resolver ?? new MobaPresentationCueResolver();
            _descriptor = descriptor;
            _definition = _resolver.Resolve(in descriptor);
        }

        public void OnConditionPassed(in TriggerCueContext context)
        {
            Publish(MobaPresentationCueStage.ConditionPassed, in context, -1);
        }

        public void OnConditionFailed(in TriggerCueContext context)
        {
            Publish(MobaPresentationCueStage.ConditionFailed, in context, -1);
        }

        public void OnBeforeAction(in TriggerCueContext context, int actionIndex)
        {
            Publish(MobaPresentationCueStage.BeforeAction, in context, actionIndex);
        }

        public void OnExecuted(in TriggerCueContext context)
        {
            Publish(MobaPresentationCueStage.Executed, in context, -1);
        }

        public void OnInterrupted(in TriggerCueContext context)
        {
            Publish(MobaPresentationCueStage.Interrupted, in context, -1);
        }

        public void OnSkipped(in TriggerCueContext context)
        {
            Publish(MobaPresentationCueStage.Skipped, in context, -1);
        }

        public void Publish(MobaPresentationCueStage stage, in TriggerCueContext context, int actionIndex = -1)
        {
            if (_snapshots == null) return;
            if (IsEmptyCue(in context)) return;

            var payload = BuildPayload(stage, in context, actionIndex);
            _snapshots.Report(in payload);
        }

        private MobaPresentationCueSnapshotEntry BuildPayload(MobaPresentationCueStage stage, in TriggerCueContext context, int actionIndex)
        {
            var cueData = context.CueData ?? context.Args;
            ResolveActors(cueData, out var sourceActorId, out var targetActorId);
            ResolveTargets(cueData, sourceActorId, targetActorId, out var targets);
            ResolvePositions(cueData, out var positions);
            ResolveLineage(cueData, out var contextKind, out var originKind, out var sourceContextId, out var rootContextId, out var ownerContextId, out var sourceConfigId);
            ResolvePresentationContext(cueData, context.CuePayload, out var requestKey, out var durationMsOverride, out var contextEventId, out var numericParamKeys, out var numericParamValues, out var stringParamKeys, out var stringParamValues);

            var descriptor = context.CueDescriptor.IsEmpty ? _descriptor : context.CueDescriptor;
            var definition = context.CueDescriptor.IsEmpty ? _definition : _resolver.Resolve(in descriptor);
            var domain = PresentationCueSemantics.ParseDomain(definition.Kind ?? descriptor.Kind);
            var lifetime = PresentationCueSemantics.ResolveLifetime(
                domain,
                (AbilityKit.Demo.Moba.Share.PresentationCueStage)stage);
            var primaryAssetId = definition.VfxId != 0 ? definition.VfxId : ParseId(descriptor.PrimaryAssetId);
            var secondaryAssetId = definition.SfxId != 0 ? definition.SfxId : ParseId(descriptor.SecondaryAssetId);
            var cueTemplateId = definition.TemplateId != 0 ? definition.TemplateId : ParseId(descriptor.CueId ?? descriptor.Kind);
            var eventKey = $"event:{context.EventId}:{context.TriggerId}:{context.ActionIndex}:{context.Order}";
            var replicationId = !string.IsNullOrWhiteSpace(requestKey) ? requestKey : eventKey;

            return new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)stage,
                CueKind = string.IsNullOrWhiteSpace(definition.Kind) ? descriptor.Kind : definition.Kind,
                CueVfxId = string.IsNullOrWhiteSpace(definition.PrimaryAssetId) ? descriptor.PrimaryAssetId : definition.PrimaryAssetId,
                CueSfxId = string.IsNullOrWhiteSpace(definition.SecondaryAssetId) ? descriptor.SecondaryAssetId : definition.SecondaryAssetId,
                TemplateId = cueTemplateId,
                VfxId = primaryAssetId,
                SfxId = secondaryAssetId,
                RequestKey = requestKey,
                Targets = targets,
                Positions = FlattenPositions(positions),
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                TriggerEventId = context.EventId,
                TriggerEventName = context.EventName,
                TriggerId = context.TriggerId,
                Phase = context.Phase,
                Priority = context.Priority,
                Order = unchecked((int)context.Order),
                ActionIndex = context.ActionIndex >= 0 ? context.ActionIndex : actionIndex,
                InterruptReason = (int)context.InterruptReason,
                InterruptSourceName = context.InterruptSourceName,
                InterruptTriggerId = context.InterruptTriggerId,
                InterruptConditionPassed = context.InterruptConditionPassed,
                DurationMsOverride = durationMsOverride,
                ContextKind = contextKind,
                OriginKind = originKind,
                SourceContextId = sourceContextId,
                RootContextId = rootContextId,
                OwnerContextId = ownerContextId,
                SourceConfigId = sourceConfigId,
                ContextEventId = contextEventId,
                NumericParamKeys = numericParamKeys,
                NumericParamValues = numericParamValues,
                StringParamKeys = stringParamKeys,
                StringParamValues = stringParamValues,
                Scale = ResolveScale(numericParamKeys, numericParamValues),
                ColorR = 1f,
                ColorG = 1f,
                ColorB = 1f,
                ColorA = 1f,
                CueLevel = (int)context.CueLevel,
                CueStage = context.CueStage != 0 ? (int)context.CueStage : (int)stage,
                ReplicationMode = (int)MobaPresentationCueReplicationMode.ReliableForLifecycle,
                ReplicationId = replicationId,
                PredictionKey = MobaPresentationCueKeys.StableHash(replicationId),
                PredictionState = (int)MobaPresentationCuePredictionState.ServerConfirmed
                ,Domain = (int)domain
                ,Lifetime = (int)lifetime
                ,SnapshotKind = (int)PresentationCueSnapshotKind.EventDelta
                ,EventKey = eventKey
            };
        }

        private bool IsEmptyCue(in TriggerCueContext context)
        {
            return _descriptor.IsEmpty && context.CueDescriptor.IsEmpty;
        }

        private static int ParseId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            return int.TryParse(value, out var id) ? id : 0;
        }

        private static void ResolveActors(object args, out int sourceActorId, out int targetActorId)
        {
            sourceActorId = 0;
            targetActorId = 0;
            if (args is IMobaActorContextProvider actorProvider)
            {
                actorProvider.TryGetSourceActorId(out sourceActorId);
                actorProvider.TryGetTargetActorId(out targetActorId);
            }
        }

        private static void ResolveLineage(object args, out int contextKind, out int originKind, out long sourceContextId, out long rootContextId, out long ownerContextId, out int sourceConfigId)
        {
            contextKind = 0;
            originKind = 0;
            sourceContextId = 0;
            rootContextId = 0;
            ownerContextId = 0;
            sourceConfigId = 0;

            if (args is IMobaTriggerLineageContextProvider lineageProvider && lineageProvider.TryGetLineageContext(out var lineage))
            {
                contextKind = (int)lineage.ContextKind;
                originKind = (int)lineage.OriginKind;
                sourceContextId = lineage.SourceContextId;
                rootContextId = lineage.RootContextId;
                ownerContextId = lineage.OwnerContextId;
                sourceConfigId = lineage.SourceConfigId;
                return;
            }

            if (args is IMobaOriginContextProvider originProvider && originProvider.TryGetOrigin(out var origin))
            {
                originKind = (int)origin.ImmediateKind;
                sourceContextId = origin.EffectiveParentContextId;
                rootContextId = origin.EffectiveRootContextId;
                ownerContextId = origin.OwnerContextId;
                sourceConfigId = origin.ImmediateConfigId;
            }
        }

        private static void ResolveTargets(object args, int sourceActorId, int targetActorId, out int[] targets)
        {
            targets = null;
            if (args is PresentationEventArgs presentation && presentation.Targets != null && presentation.Targets.Length > 0)
            {
                targets = presentation.Targets;
                return;
            }

            if (targetActorId > 0)
            {
                targets = new[] { targetActorId };
                return;
            }

            if (sourceActorId > 0)
            {
                targets = new[] { sourceActorId };
            }
        }

        private static void ResolvePresentationContext(
            object args,
            string cuePayload,
            out string requestKey,
            out int durationMsOverride,
            out string contextEventId,
            out int[] numericParamKeys,
            out float[] numericParamValues,
            out string[] stringParamKeys,
            out string[] stringParamValues)
        {
            requestKey = cuePayload;
            durationMsOverride = 0;
            contextEventId = null;
            numericParamKeys = null;
            numericParamValues = null;
            stringParamKeys = null;
            stringParamValues = null;

            if (args is PresentationEventArgs presentation)
            {
                requestKey = string.IsNullOrWhiteSpace(presentation.RequestKey) ? requestKey : presentation.RequestKey;
                durationMsOverride = presentation.DurationMsOverride;
                contextEventId = presentation.EventId;
                BuildPresentationParams(presentation, out numericParamKeys, out numericParamValues, out stringParamKeys, out stringParamValues);
            }
        }

        private static void BuildPresentationParams(
            PresentationEventArgs presentation,
            out int[] numericParamKeys,
            out float[] numericParamValues,
            out string[] stringParamKeys,
            out string[] stringParamValues)
        {
            numericParamKeys = null;
            numericParamValues = null;
            stringParamKeys = null;
            stringParamValues = null;

            int numericCount = 0;
            var scale = TryReadFloat(presentation.Scale, out var scaleValue);
            var radius = TryReadFloat(presentation.Radius, out var radiusValue);
            if (scale) numericCount++;
            if (radius) numericCount++;

            if (numericCount > 0)
            {
                numericParamKeys = new int[numericCount];
                numericParamValues = new float[numericCount];
                int index = 0;
                if (scale)
                {
                    numericParamKeys[index] = NumericParamScale;
                    numericParamValues[index++] = scaleValue;
                }

                if (radius)
                {
                    numericParamKeys[index] = NumericParamRadius;
                    numericParamValues[index] = radiusValue;
                }
            }

            if (presentation.Color is string color && !string.IsNullOrWhiteSpace(color))
            {
                stringParamKeys = new[] { StringParamColor };
                stringParamValues = new[] { color };
            }
        }

        private static bool TryReadFloat(object value, out float result)
        {
            switch (value)
            {
                case float f:
                    result = f;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case int i:
                    result = i;
                    return true;
                default:
                    result = 0f;
                    return false;
            }
        }

        private static float ResolveScale(int[] numericParamKeys, float[] numericParamValues)
        {
            if (numericParamKeys == null || numericParamValues == null) return 1f;
            var count = Math.Min(numericParamKeys.Length, numericParamValues.Length);
            for (int i = 0; i < count; i++)
            {
                if (numericParamKeys[i] == NumericParamScale && numericParamValues[i] > 0f)
                {
                    return numericParamValues[i];
                }
            }

            return 1f;
        }

        private static void ResolvePositions(object args, out Vec3[] positions)
        {
            positions = null;
            if (args is PresentationEventArgs presentation && presentation.Positions != null && presentation.Positions.Length > 0)
            {
                positions = presentation.Positions;
            }
        }

        private static float[] FlattenPositions(Vec3[] positions)
        {
            if (positions == null || positions.Length == 0) return null;

            var values = new float[positions.Length * 3];
            for (int i = 0; i < positions.Length; i++)
            {
                int offset = i * 3;
                values[offset] = positions[i].X;
                values[offset + 1] = positions[i].Y;
                values[offset + 2] = positions[i].Z;
            }

            return values;
        }
    }
}
