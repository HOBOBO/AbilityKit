using System;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services.Triggering.Cue
{
    /// <summary>
    /// Converts a logic-layer presentation request into the snapshot contract consumed
    /// by the view runtime.  The event bus remains the local request path; this reporter
    /// supplies the authoritative cross-world/state-sync path.
    /// </summary>
    public static class MobaPresentationRequestReporter
    {
        public static void ReportPlay(
            MobaPresentationCueSnapshotService snapshots,
            MobaConfigDatabase configs,
            PresentationEventArgs payload)
        {
            if (snapshots == null || payload == null || payload.TemplateId <= 0) return;

            var persistent = !string.IsNullOrWhiteSpace(payload.RequestKey);
            var stage = persistent ? MobaPresentationCueStage.Started : MobaPresentationCueStage.Executed;
            var lifetime = persistent ? PresentationCueLifetime.PersistentState : PresentationCueLifetime.TransientEvent;
            var entry = BuildEntry(configs, payload, stage, lifetime);
            snapshots.Report(in entry);
        }

        /// <summary>
        /// Reports the stop side of a keyed presentation request.  One-shot
        /// requests have no retained instance to stop, so they intentionally do
        /// not emit a synthetic stop event.
        /// </summary>
        public static void ReportStop(
            MobaPresentationCueSnapshotService snapshots,
            MobaConfigDatabase configs,
            PresentationEventArgs payload)
        {
            if (snapshots == null || payload == null || payload.TemplateId <= 0 ||
                string.IsNullOrWhiteSpace(payload.RequestKey)) return;

            var entry = BuildEntry(
                configs,
                payload,
                MobaPresentationCueStage.Removed,
                PresentationCueLifetime.PersistentState);
            snapshots.Report(in entry);
        }

        private static MobaPresentationCueSnapshotEntry BuildEntry(
            MobaConfigDatabase configs,
            PresentationEventArgs payload,
            MobaPresentationCueStage stage,
            PresentationCueLifetime lifetime)
        {

            PresentationTemplateMO template = null;
            if (configs != null)
            {
                configs.GetTable<PresentationTemplateMO>().TryGet(payload.TemplateId, out template);
            }

            var domain = PresentationCueDomain.Effect;
            return new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)stage,
                CueKind = "effect.presentation",
                OwnerKind = "Effect",
                TemplateId = payload.TemplateId,
                VfxId = template != null ? template.AssetId : payload.TemplateId,
                RequestKey = payload.RequestKey,
                SourceActorId = payload.SourceActorId,
                TargetActorId = payload.TargetActorId,
                Targets = payload.Targets,
                Positions = FlattenPositions(payload.Positions),
                OffsetX = template != null ? template.OffsetX : 0f,
                OffsetY = template != null ? template.OffsetY : 0f,
                OffsetZ = template != null ? template.OffsetZ : 0f,
                DurationMsOverride = payload.DurationMsOverride > 0
                    ? payload.DurationMsOverride
                    : template != null ? template.DefaultDurationMs : 0,
                Scale = ResolveScale(payload.Scale, template),
                SourceContextId = payload.SourceContextId,
                RootContextId = payload.RootContextId,
                OwnerContextId = payload.OwnerContextId,
                SourceConfigId = payload.TemplateId,
                ContextEventId = payload.EventId,
                Domain = (int)domain,
                Lifetime = (int)lifetime,
                SnapshotKind = (int)PresentationCueSnapshotKind.EventDelta,
                ReplicationMode = (int)MobaPresentationCueReplicationMode.ReliableForLifecycle,
                PredictionState = (int)MobaPresentationCuePredictionState.ServerConfirmed,
                NumericParamKeys = BuildNumericKeys(payload),
                NumericParamValues = BuildNumericValues(payload),
                StringParamKeys = BuildStringKeys(payload),
                StringParamValues = BuildStringValues(payload),
            };
        }

        private static float ResolveScale(object value, PresentationTemplateMO template)
        {
            if (value is float f && f > 0f) return f;
            if (value is double d && d > 0d) return (float)d;
            if (value is int i && i > 0) return i;
            return template != null && template.Scale > 0f ? template.Scale : 1f;
        }

        private static float[] FlattenPositions(AbilityKit.Core.Mathematics.Vec3[] positions)
        {
            if (positions == null || positions.Length == 0) return null;
            var result = new float[positions.Length * 3];
            for (var i = 0; i < positions.Length; i++)
            {
                var offset = i * 3;
                result[offset] = positions[i].X;
                result[offset + 1] = positions[i].Y;
                result[offset + 2] = positions[i].Z;
            }
            return result;
        }

        private static int[] BuildNumericKeys(PresentationEventArgs payload)
        {
            var count = payload.Radius != null ? 1 : 0;
            return count == 0 ? null : new[] { 2 };
        }

        private static float[] BuildNumericValues(PresentationEventArgs payload)
        {
            if (!(payload.Radius is float radius) || radius <= 0f) return null;
            return new[] { radius };
        }

        private static string[] BuildStringKeys(PresentationEventArgs payload)
        {
            return payload.Color is string color && !string.IsNullOrWhiteSpace(color)
                ? new[] { "color" }
                : null;
        }

        private static string[] BuildStringValues(PresentationEventArgs payload)
        {
            return payload.Color is string color && !string.IsNullOrWhiteSpace(color)
                ? new[] { color }
                : null;
        }
    }
}
