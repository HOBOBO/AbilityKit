using System;
using System.Collections.Generic;
using AbilityKit.ActionSchema;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public sealed class MobaTimelinePlayer
    {
        private readonly SkillAssetDto _asset;
        private readonly MobaClipHandlerRegistry _registry;
        private readonly IMobaTimelineEventSink _sink;

        // Q32.32 raw 时间累计（整数加法无漂移）；float Time 是边界视图。
        private long _timeRaw;
        private readonly HashSet<string> _fired = new HashSet<string>();

        public MobaTimelinePlayer(SkillAssetDto asset, MobaClipHandlerRegistry registry, IMobaTimelineEventSink sink)
        {
            _asset = asset;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _sink = sink;
        }

        public float Time => Deterministic.Fixed64.FromRaw(_timeRaw).ToSingle();

        public void Reset(float time = 0f)
        {
            _timeRaw = DeterministicMathBridge.ToFixed(time).RawValue;
            _fired.Clear();
        }

        public void Update(float deltaTime)
        {
            if (_asset == null || _asset.groups == null) return;

            if (deltaTime > 0f)
            {
                _timeRaw += DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            }

            var epsilonRaw = DeterministicMathBridge.ToFixed(1e-6f).RawValue;
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

                        if (_timeRaw + epsilonRaw < DeterministicMathBridge.ToFixed(clip.start).RawValue) continue;

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
            handler.TryHandle(Time, clip, _sink);
        }
    }
}
