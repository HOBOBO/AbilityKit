using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AbilityKit.ActionSchema;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaActionTimelineCompiler
    {
        private const string ExecuteEffectType = "AbilityKit.ActionEditorImpl.ExecuteEffect";
        private const string TriggerLogType = "AbilityKit.ActionEditorImpl.TriggerLog";

        public static SkillTimelinePhaseDTO CompileLogicPhase(string json)
        {
            return CompileLogicPhase(ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Logic));
        }

        public static SkillTimelinePhaseDTO CompileLogicPhase(SkillAssetDto asset)
        {
            if (asset == null || asset.schemaVersion != 1 || asset.runtimeType != ActionTimelineRuntimeTypes.Logic)
                throw new InvalidDataException("A versioned MOBA logic timeline is required.");
            ActionTimelinePartition.Validate(asset, ActionTimelineRuntimeTypes.Logic);

            var events = new List<SkillTimelineEventDTO>();
            foreach (var group in asset.groups)
            {
                if (group == null || !group.active || group.tracks == null) continue;
                if (group.actorId != 0)
                    throw new InvalidDataException("Logic timeline actor binding is not yet supported: " + group.actorId);
                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active || track.clips == null) continue;
                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;
                        if (clip.type == TriggerLogType) continue; // Debug-only; no authoritative effect.
                        if (clip.type != ExecuteEffectType || clip.args == null ||
                            !clip.args.TryGetValue("effectId", out var raw) ||
                            !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var effectId) || effectId <= 0)
                            throw new InvalidDataException("Unsupported or invalid MOBA logic clip: " + clip.type);

                        events.Add(new SkillTimelineEventDTO
                        {
                            AtMs = ToMilliseconds(clip.start),
                            EffectId = effectId,
                            ExecuteMode = 0,
                            EventTag = clip.args.TryGetValue("eventTag", out var tag) ? tag : null
                        });
                    }
                }
            }

            // List.Sort is not stable; retain authoring order for simultaneous effects.
            var ordered = new List<(SkillTimelineEventDTO Event, int Index)>(events.Count);
            for (var i = 0; i < events.Count; i++) ordered.Add((events[i], i));
            ordered.Sort((a, b) =>
            {
                var time = a.Event.AtMs.CompareTo(b.Event.AtMs);
                return time != 0 ? time : a.Index.CompareTo(b.Index);
            });
            for (var i = 0; i < ordered.Count; i++) events[i] = ordered[i].Event;

            return new SkillTimelinePhaseDTO
            {
                DurationMs = ToMilliseconds(asset.length),
                Events = events.ToArray()
            };
        }

        private static int ToMilliseconds(float seconds)
        {
            var milliseconds = Math.Round((double)seconds * 1000, MidpointRounding.AwayFromZero);
            if (milliseconds < 0 || milliseconds > int.MaxValue)
                throw new InvalidDataException("MOBA timeline time is outside the millisecond range.");
            return (int)milliseconds;
        }
    }
}
