using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.ActionSchema;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Battle.Presentation.Timeline
{
    public interface IMobaSkillPresentationTimelineSink
    {
        void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds);
        void OnTimelineStop(long skillInstanceId);
    }

    public interface IMobaSkillPresentationRequestSink
    {
        void OnPresentationRequest(in PresentationRequest request, GroupDto group, ClipDto clip, float offsetSeconds);
    }

    public interface IMobaSkillPresentationAsyncRequestSink
    {
        Task OnPresentationRequestAsync(
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds);
    }

    /// <summary>
    /// Optional cancellable form for consumers that load resources asynchronously.
    /// The non-cancellable interface remains supported for compatibility.
    /// </summary>
    public interface IMobaSkillPresentationCancellableAsyncRequestSink
    {
        Task OnPresentationRequestAsync(
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds,
            CancellationToken cancellationToken);
    }

    public sealed class MobaSkillPresentationTimelineRuntime
    {
        private const string AnimationType = "AbilityKit.ActionEditorImpl.PlayAnimation";
        private const string ParticleType = "AbilityKit.ActionEditorImpl.PlayParticle";
        private readonly List<(GroupDto Group, ClipDto Clip, int Index)> _clips = new List<(GroupDto, ClipDto, int)>();
        private readonly IMobaSkillPresentationTimelineSink _sink;
        private readonly CancellationTokenSource _dispatchCancellation = new CancellationTokenSource();
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
            _dispatchCancellation.Cancel();
            _dispatchCancellation.Dispose();
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
                var request = CreateRequest(entry.Group, entry.Clip, entry.Index, offset);
                if (_sink is IMobaSkillPresentationCancellableAsyncRequestSink cancellableSink)
                    _ = DispatchAsync(cancellableSink, request, entry.Group, entry.Clip, offset, _dispatchCancellation.Token);
                else if (_sink is IMobaSkillPresentationAsyncRequestSink asyncRequestSink)
                    _ = DispatchAsync(asyncRequestSink, request, entry.Group, entry.Clip, offset);
                else if (_sink is IMobaSkillPresentationRequestSink requestSink)
                    requestSink.OnPresentationRequest(in request, entry.Group, entry.Clip, offset);
                else
                    _sink.OnClipStart(_skillInstanceId, entry.Group, entry.Clip, offset);
            }
        }

        private static async Task DispatchAsync(
            IMobaSkillPresentationAsyncRequestSink sink,
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds)
        {
            try
            {
                await sink.OnPresentationRequestAsync(request, group, clip, offsetSeconds);
            }
            catch (Exception exception)
            {
                // Timeline dispatch is intentionally fire-and-forget. Keep an
                // async consumer failure observable without creating an
                // unobserved task exception on the render loop.
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

        private static async Task DispatchAsync(
            IMobaSkillPresentationCancellableAsyncRequestSink sink,
            PresentationRequest request,
            GroupDto group,
            ClipDto clip,
            float offsetSeconds,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await sink.OnPresentationRequestAsync(
                    request,
                    group,
                    clip,
                    offsetSeconds,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

        private PresentationRequest CreateRequest(
            GroupDto group,
            ClipDto clip,
            int index,
            float offset)
        {
            var resource = clip.args[clip.type == AnimationType ? "clipKey" : "resourceKey"];
            var durationMs = clip.length > 0 ? (int)Math.Max(1, clip.length * 1000f) : 0;
            return new PresentationRequest(
                "timeline:" + _skillInstanceId + ":" + index,
                assetId: 0,
                position: new PresentationVector3(0f, 1.1f, 0.55f),
                attachment: new PresentationAttachment(PresentationAttachmentKind.Actor, group.actorId),
                durationMs: durationMs,
                scale: 0.55f,
                parameters: new[] { new PresentationParameter("offsetSeconds", offset) },
                resourceKey: clip.type == AnimationType ? string.Empty : resource,
                assetKind: clip.type == AnimationType ? PresentationAssetKind.Animation : PresentationAssetKind.Particle,
                playback: clip.type == AnimationType
                    ? new PresentationPlaybackSpec(clipKey: resource, normalizedTime: offset > 0f ? offset / Math.Max(clip.length, 0.0001f) : 0f)
                    : PresentationPlaybackSpec.Default);
        }
    }
}
