using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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

    public interface IMobaPresentationTemplateResolver
    {
        bool TryCreateRequest(int templateId, string logicalKey, GroupDto group, int durationMs,
            float offsetSeconds, out PresentationRequest request);
    }

    public sealed class MobaSkillPresentationTimelineRuntime
    {
        private const string AnimationType = "AbilityKit.ActionEditorImpl.PlayAnimation";
        private const string ParticleType = "AbilityKit.ActionEditorImpl.PlayParticle";
        private readonly List<(GroupDto Group, ClipDto Clip, int Index)> _clips = new List<(GroupDto, ClipDto, int)>();
        private readonly IMobaSkillPresentationTimelineSink _sink;
        private readonly CancellationTokenSource _dispatchCancellation = new CancellationTokenSource();
        private readonly long _skillInstanceId;
        private readonly IMobaPresentationTemplateResolver _templateResolver;
        private readonly PresentationRateResolver _rateResolver;
        private int _next;
        private float _time;
        private bool _started;
        private bool _stopped;

        public MobaSkillPresentationTimelineRuntime(long skillInstanceId, string json, IMobaSkillPresentationTimelineSink sink,
            IMobaPresentationTemplateResolver templateResolver = null,
            PresentationRateResolver rateResolver = null)
            : this(skillInstanceId, ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Presentation), sink, templateResolver, rateResolver)
        {
        }

        public MobaSkillPresentationTimelineRuntime(long skillInstanceId, SkillAssetDto asset, IMobaSkillPresentationTimelineSink sink,
            IMobaPresentationTemplateResolver templateResolver = null,
            PresentationRateResolver rateResolver = null)
        {
            if (skillInstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(skillInstanceId));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            if (asset == null || asset.schemaVersion != 1 || asset.runtimeType != ActionTimelineRuntimeTypes.Presentation)
                throw new InvalidDataException("A versioned MOBA presentation timeline is required.");
            ActionTimelinePartition.Validate(asset, ActionTimelineRuntimeTypes.Presentation);
            _skillInstanceId = skillInstanceId;
            _templateResolver = templateResolver;
            _rateResolver = rateResolver;

            foreach (var group in asset.groups)
            {
                if (group == null || !group.active || group.tracks == null) continue;
                foreach (var track in group.tracks)
                {
                    if (track == null || !track.active || track.clips == null) continue;
                    foreach (var clip in track.clips)
                    {
                        if (clip == null) continue;
                        var valid = clip.args != null && (clip.type == AnimationType
                            ? clip.args.TryGetValue("clipKey", out var key) && !string.IsNullOrWhiteSpace(key)
                            : clip.type == ParticleType &&
                              (clip.args.TryGetValue("presentationTemplateId", out var id) && int.TryParse(id, out var templateId) && templateId > 0 ||
                               clip.args.TryGetValue("resourceKey", out var legacy) && !string.IsNullOrWhiteSpace(legacy)));
                        if (!valid)
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
            var effectiveDelta = _rateResolver == null
                ? deltaSeconds
                : _rateResolver.Resolve(deltaSeconds, PresentationRateChannel.SkillTimeline).DeltaSeconds;
            Advance(effectiveDelta);
        }

        public void TickUnscaled(float deltaSeconds)
        {
            if (_stopped) return;
            if (!_started) throw new InvalidOperationException("Timeline has not started.");
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            Advance(deltaSeconds);
        }

        private void Advance(float deltaSeconds)
        {
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
                System.Diagnostics.Trace.WriteLine("[MobaPresentation] Dispatch skill=" + _skillInstanceId +
                    " clip=" + entry.Clip.type + " start=" + entry.Clip.start +
                    " time=" + _time + " offset=" + offset);
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
            var isAnimation = clip.type == AnimationType;
            var resource = isAnimation ? clip.args["clipKey"] : string.Empty;
            var speed = ReadAnimationNumber(clip, "playbackSpeed", 1f);
            var clipOffset = ReadAnimationNumber(clip, "clipOffset", 0f);
            var sourceLength = ReadAnimationNumber(clip, "clipLength", clip.length);
            var durationMs = clip.length > 0 ? (int)Math.Max(1, clip.length * 1000f) : 0;
            if (!isAnimation && _templateResolver != null &&
                clip.args.TryGetValue("presentationTemplateId", out var templateText) &&
                int.TryParse(templateText, out var templateId) &&
                _templateResolver.TryCreateRequest(templateId,
                    "timeline:" + _skillInstanceId + ":" + index, group, durationMs, offset, out var configured))
                return configured;
            if (!isAnimation && clip.args.ContainsKey("presentationTemplateId"))
                throw new InvalidDataException("Unable to resolve MOBA presentation template for PlayParticle.");
            if (!isAnimation) resource = clip.args["resourceKey"];
            return new PresentationRequest(
                "timeline:" + _skillInstanceId + ":" + index,
                assetId: 0,
                position: new PresentationVector3(0f, 1.1f, 0.55f),
                attachment: new PresentationAttachment(PresentationAttachmentKind.Actor, group.actorId),
                durationMs: durationMs,
                scale: 0.55f,
                parameters: new[] { new PresentationParameter("offsetSeconds", offset) },
                resourceKey: isAnimation ? string.Empty : resource,
                assetKind: isAnimation ? PresentationAssetKind.Animation : PresentationAssetKind.Particle,
                playback: isAnimation
                    ? new PresentationPlaybackSpec(clipKey: resource, speed: speed,
                        normalizedTime: Math.Max(0f, clipOffset + offset * speed) / Math.Max(sourceLength, 0.0001f))
                    : PresentationPlaybackSpec.Default);
        }

        private static float ReadAnimationNumber(ClipDto clip, string name, float fallback)
        {
            return clip.args.TryGetValue(name, out var text) &&
                   float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                   !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f
                ? value : fallback;
        }
    }
}
