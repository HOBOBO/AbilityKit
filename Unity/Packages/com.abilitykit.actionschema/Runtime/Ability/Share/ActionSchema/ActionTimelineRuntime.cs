using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AbilityKit.ActionSchema
{
    public static class ActionTimelineJson
    {
        public static SkillAssetDto LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException(nameof(path));
            var json = File.ReadAllText(path);
            return LoadFromJson(json);
        }

        public static SkillAssetDto LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // 默认忽略类似 "$type" 的额外字段。
            return JsonConvert.DeserializeObject<SkillAssetDto>(json);
        }

        public static SkillAssetDto LoadForRuntime(string json, string runtimeType)
        {
            var asset = LoadFromJson(json);
            if (asset == null || asset.schemaVersion != 1 || asset.runtimeType != runtimeType)
                throw new InvalidDataException("Timeline runtime type or schema version does not match.");

            ActionTimelinePartition.Validate(asset, runtimeType);
            return asset;
        }
    }

    public static class ActionTimelinePartition
    {
        public static SkillAssetDto Create(SkillAssetDto source, string runtimeType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            CheckRuntimeType(runtimeType);
            if (source.groups == null || float.IsNaN(source.length) || float.IsInfinity(source.length) || source.length < 0)
                throw new InvalidDataException("Timeline has invalid groups or length.");

            var result = new SkillAssetDto { schemaVersion = 1, runtimeType = runtimeType, length = source.length };
            foreach (var group in source.groups)
            {
                if (group == null || !group.active) continue;
                if (group.tracks == null) throw new InvalidDataException("Active timeline group has no tracks.");
                var outputGroup = new GroupDto { name = group.name, actorId = group.actorId, active = true };
                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active) continue;
                    if (track.clips == null) throw new InvalidDataException("Active timeline track has no clips.");
                    var outputTrack = new TrackDto { type = track.type, name = track.name, active = true };
                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;
                        if (clip.runtimeType != ActionTimelineRuntimeTypes.Logic &&
                            clip.runtimeType != ActionTimelineRuntimeTypes.Presentation)
                            throw new InvalidDataException("Active timeline clip has no supported runtime type: " + clip.type);
                        ValidateClip(clip, source.length);
                        if (clip.runtimeType != runtimeType) continue;
                        outputTrack.clips.Add(new ClipDto
                        {
                            type = clip.type,
                            runtimeType = clip.runtimeType,
                            start = clip.start,
                            length = clip.length,
                            blendIn = clip.blendIn,
                            blendOut = clip.blendOut,
                            args = clip.args != null
                                ? new Dictionary<string, string>(clip.args)
                                : new Dictionary<string, string>()
                        });
                    }
                    if (outputTrack.clips.Count > 0) outputGroup.tracks.Add(outputTrack);
                }
                if (outputGroup.tracks.Count > 0) result.groups.Add(outputGroup);
            }
            return result;
        }

        public static void Validate(SkillAssetDto asset, string runtimeType)
        {
            CheckRuntimeType(runtimeType);
            if (asset == null || asset.groups == null || float.IsNaN(asset.length) ||
                float.IsInfinity(asset.length) || asset.length < 0)
                throw new InvalidDataException("Timeline has invalid groups or length.");
            foreach (var group in asset.groups)
            {
                if (group == null || !group.active) continue;
                if (group.tracks == null) throw new InvalidDataException("Active timeline group has no tracks.");
                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active) continue;
                    if (track.clips == null) throw new InvalidDataException("Active timeline track has no clips.");
                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;
                        if (clip.runtimeType != runtimeType)
                            throw new InvalidDataException("Timeline contains a clip from another runtime: " + clip.type);
                        ValidateClip(clip, asset.length);
                    }
                }
            }
        }

        private static void CheckRuntimeType(string runtimeType)
        {
            if (runtimeType != ActionTimelineRuntimeTypes.Logic && runtimeType != ActionTimelineRuntimeTypes.Presentation)
                throw new ArgumentException("Unknown timeline runtime type.", nameof(runtimeType));
        }

        private static void ValidateClip(ClipDto clip, float length)
        {
            if (string.IsNullOrEmpty(clip.type) || float.IsNaN(clip.start) || float.IsInfinity(clip.start) ||
                clip.start < 0 || clip.start > length || float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length) || clip.length < 0 ||
                float.IsNaN(clip.blendIn) || float.IsInfinity(clip.blendIn) || clip.blendIn < 0 ||
                float.IsNaN(clip.blendOut) || float.IsInfinity(clip.blendOut) || clip.blendOut < 0)
                throw new InvalidDataException("Timeline clip has invalid type or time: " + clip.type);
        }
    }

    public interface ITimelineEventSink
    {
        void OnTriggerLog(float time, string message);
    }

    public sealed class TimelinePlayer
    {
        private readonly SkillAssetDto _asset;
        private readonly ITimelineEventSink _sink;

        private float _time;
        private readonly HashSet<string> _fired = new HashSet<string>();

        public TimelinePlayer(SkillAssetDto asset, ITimelineEventSink sink)
        {
            _asset = asset;
            _sink = sink;
        }

        public float Time => _time;

        public void Reset(float time = 0f)
        {
            _time = time;
            _fired.Clear();
        }

        public void Update(float deltaTime)
        {
            if (_asset == null || _asset.groups == null) return;

            if (deltaTime < 0) deltaTime = 0;
            _time += deltaTime;

            foreach (var group in _asset.groups)
            {
                if (group == null || !group.active) continue;
                if (group.tracks == null) continue;

                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active) continue;
                    if (track.clips == null) continue;

                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;

                        // 每个片段只触发一次。
                        var key = MakeClipKey(group, track, clip);
                        if (_fired.Contains(key)) continue;

                        if (_time + 1e-6f < clip.start) continue;

                        TryFireClip(clip);
                        _fired.Add(key);
                    }
                }
            }
        }

        private static string MakeClipKey(GroupDto group, TrackDto track, ClipDto clip)
        {
            // 对运行时内存态足够稳定，可避免额外依赖 ID。
            return (group.name ?? string.Empty) + "|" + (track.name ?? string.Empty) + "|" + (clip.type ?? string.Empty) + "|" + clip.start.ToString("R") + "|" + clip.length.ToString("R");
        }

        private void TryFireClip(ClipDto clip)
        {
            if (_sink == null) return;

            // 当前测试用例。
            if (IsTriggerLog(clip.type))
            {
                string msg = null;
                if (clip.args != null)
                {
                    clip.args.TryGetValue("log", out msg);
                }

                _sink.OnTriggerLog(_time, msg ?? string.Empty);
            }
        }

        private static bool IsTriggerLog(string type)
        {
            if (string.IsNullOrEmpty(type)) return false;
            return type.EndsWith(".TriggerLog", StringComparison.Ordinal) || type == "AbilityKit.ActionEditorImpl.TriggerLog";
        }
    }
}
