using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.ActionSchema;

namespace AbilityKit.Game.Flow.Battle.Presentation.Timeline
{
    public interface IMobaSkillPresentationTimelineSink
    {
        void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds);
        void OnTimelineStop(long skillInstanceId);
    }

    public sealed class MobaSkillPresentationTimelineRuntime
    {
        private const string AnimationType = "AbilityKit.ActionEditorImpl.PlayAnimation";
        private const string ParticleType = "AbilityKit.ActionEditorImpl.PlayParticle";
        private readonly List<(GroupDto Group, ClipDto Clip, int Index)> _clips = new List<(GroupDto, ClipDto, int)>();
        private readonly IMobaSkillPresentationTimelineSink _sink;
        private readonly long _skillInstanceId;
        private int _next;
        private float _time;
        private bool _started;
        private bool _stopped;

        public MobaSkillPresentationTimelineRuntime(long skillInstanceId, string json, IMobaSkillPresentationTimelineSink sink)
            : this(skillInstanceId, ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Presentation), sink)
        {
        }

        public MobaSkillPresentationTimelineRuntime(long skillInstanceId, SkillAssetDto asset, IMobaSkillPresentationTimelineSink sink)
        {
            if (skillInstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(skillInstanceId));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            if (asset == null || asset.schemaVersion != 1 || asset.runtimeType != ActionTimelineRuntimeTypes.Presentation)
                throw new InvalidDataException("A versioned MOBA presentation timeline is required.");
            ActionTimelinePartition.Validate(asset, ActionTimelineRuntimeTypes.Presentation);
            _skillInstanceId = skillInstanceId;

            foreach (var group in asset.groups)
            {
                if (group == null || !group.active || group.tracks == null) continue;
                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active || track.clips == null) continue;
                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;
                        var keyName = clip.type == AnimationType ? "clipKey" :
                            clip.type == ParticleType ? "resourceKey" : null;
                        if (keyName == null || clip.args == null ||
                            !clip.args.TryGetValue(keyName, out var key) || string.IsNullOrWhiteSpace(key))
                            throw new InvalidDataException("Unsupported or invalid MOBA presentation clip: " + clip.type);
                        _clips.Add((group, clip, _clips.Count));
                    }
                }
            }
            _clips.Sort((a, b) =>
            {
                var time = a.Clip.start.CompareTo(b.Clip.start);
                return time != 0 ? time : a.Index.CompareTo(b.Index);
            });
        }

        public float Time => _time;
        public bool IsStopped => _stopped;

        public void StartAt(float elapsedSeconds)
        {
            if (_stopped || _started) throw new InvalidOperationException("Timeline already started or stopped.");
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            _time = elapsedSeconds;
            _started = true;
            DispatchDueClips();
        }

        public void Tick(float deltaSeconds)
        {
            if (_stopped) return;
            if (!_started) throw new InvalidOperationException("Timeline has not started.");
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _time += deltaSeconds;
            DispatchDueClips();
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            _sink.OnTimelineStop(_skillInstanceId);
        }

        private void DispatchDueClips()
        {
            while (_next < _clips.Count && _clips[_next].Clip.start <= _time)
            {
                var entry = _clips[_next++];
                var offset = Math.Max(0, _time - entry.Clip.start);
                if (entry.Clip.length > 0 && offset >= entry.Clip.length) continue;
                if (entry.Clip.length == 0 && offset > 0) continue;
                _sink.OnClipStart(_skillInstanceId, entry.Group, entry.Clip, offset);
            }
        }
    }
}
