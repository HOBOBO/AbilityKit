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

            // Extra fields like "$type" will be ignored by default.
            return JsonConvert.DeserializeObject<SkillAssetDto>(json);
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

                        // Only fire once.
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
            // Stable enough for runtime in-memory; avoids needing IDs.
            return (group.name ?? string.Empty) + "|" + (track.name ?? string.Empty) + "|" + (clip.type ?? string.Empty) + "|" + clip.start.ToString("R") + "|" + clip.length.ToString("R");
        }

        private void TryFireClip(ClipDto clip)
        {
            if (_sink == null) return;

            // Current test case
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
