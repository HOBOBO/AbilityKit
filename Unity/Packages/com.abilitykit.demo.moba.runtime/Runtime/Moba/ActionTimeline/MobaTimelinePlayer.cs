using System;
using System.Collections.Generic;
using AbilityKit.ActionSchema;

namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public sealed class MobaTimelinePlayer
    {
        private readonly SkillAssetDto _asset;
        private readonly MobaClipHandlerRegistry _registry;
        private readonly IMobaTimelineEventSink _sink;

        private float _time;
        private readonly HashSet<string> _fired = new HashSet<string>();

        public MobaTimelinePlayer(SkillAssetDto asset, MobaClipHandlerRegistry registry, IMobaTimelineEventSink sink)
        {
            _asset = asset;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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
            return (group.name ?? string.Empty) + "|" + (track.name ?? string.Empty) + "|" + (clip.type ?? string.Empty) + "|" + clip.start.ToString("R") + "|" + clip.length.ToString("R");
        }

        private void TryFireClip(ClipDto clip)
        {
            if (_sink == null) return;

            if (!_registry.TryGet(clip.type, out var handler) || handler == null) return;
            handler.TryHandle(_time, clip, _sink);
        }
    }
}
